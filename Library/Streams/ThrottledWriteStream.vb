'''<summary>
'''Stops the substream from being written to too quickly by queueing write calls.
'''Doesn't block the caller.
'''</summary>
Public Class ThrottledWriteStream
    Inherits WrappedStream
    Private ReadOnly writer As ThrottledWriter

    Public Sub New(ByVal substream As IO.Stream,
                   Optional ByVal initialSlack As Double = 0,
                   Optional ByVal costPerWrite As Double = 0,
                   Optional ByVal costPerCharacter As Double = 0,
                   Optional ByVal costLimit As Double = 0,
                   Optional ByVal costRecoveredPerSecond As Double = 1)
        MyBase.New(substream)
        Contract.Requires(substream IsNot Nothing)
        Contract.Requires(initialSlack >= 0)
        Contract.Requires(costPerWrite >= 0)
        Contract.Requires(costPerCharacter >= 0)
        Contract.Requires(costLimit >= 0)
        Contract.Requires(costRecoveredPerSecond > 0)
        writer = New ThrottledWriter(substream, initialSlack, costPerWrite, costPerCharacter, costLimit, costRecoveredPerSecond)
    End Sub

    Public Overrides ReadOnly Property CanSeek() As Boolean
        Get
            Return False
        End Get
    End Property
    Public Overrides Property Position() As Long
        Get
            Return substream.Position
        End Get
        Set(ByVal value As Long)
            Throw New NotSupportedException
        End Set
    End Property
    Public Overrides Function Seek(ByVal offset As Long, ByVal origin As System.IO.SeekOrigin) As Long
        Throw New NotSupportedException
    End Function
    Public Overrides Sub SetLength(ByVal value As Long)
        Throw New NotSupportedException
    End Sub

    '''<summary>Queues a write to the substream. Doesn't block.</summary>
    Public Overrides Sub Write(ByVal buffer() As Byte, ByVal offset As Integer, ByVal count As Integer)
        writer.QueueWrite(SubArray(buffer, offset, count))
    End Sub
End Class

Public Class ThrottledReader
    Inherits WrappedStream

    Private ReadOnly costPerRead As Double
    Private ReadOnly costPerCharacter As Double
    Private ReadOnly costLimit As Double
    Private ReadOnly recoveryRate As Double

    Private availableSlack As Double
    Private usedCost As Double
    Private lastReadTime As Date = DateTime.Now()

    Public Sub New(ByVal substream As IO.Stream,
                   Optional ByVal initialSlack As Double = 0,
                   Optional ByVal costPerRead As Double = 0,
                   Optional ByVal costPerCharacter As Double = 0,
                   Optional ByVal costLimit As Double = 0,
                   Optional ByVal costRecoveredPerSecond As Double = 1)
        MyBase.New(substream)
        Contract.Requires(substream IsNot Nothing)
        Contract.Requires(initialSlack >= 0)
        Contract.Requires(costPerRead >= 0)
        Contract.Requires(costPerCharacter >= 0)
        Contract.Requires(costLimit >= 0)
        Contract.Requires(costRecoveredPerSecond > 0)

        Me.availableSlack = initialSlack
        Me.costPerRead = costPerRead
        Me.costPerCharacter = costPerCharacter
        Me.costLimit = costLimit
        Me.recoveryRate = costRecoveredPerSecond / TimeSpan.TicksPerSecond
    End Sub

    Public Overrides Function Read(ByVal buffer() As Byte, ByVal offset As Integer, ByVal count As Integer) As Integer
        Dim n = MyBase.Read(buffer, offset, count)
        usedCost += costPerRead
        usedCost += costPerCharacter * n
        Dim t = Now()
        Dim dt = t - lastReadTime
        lastReadTime = t
        usedCost -= dt.Ticks * recoveryRate
    End Function
End Class
Public Class ThrottledWriter
    Inherits AbstractLockFreeConsumer(Of Byte())

    Private ReadOnly destinationStream As IO.Stream
    Private ReadOnly consumptionQueue As New Queue(Of Byte())
    Private ReadOnly ref As ICallQueue = New ThreadPooledCallQueue

    Private ReadOnly costPerWrite As Double
    Private ReadOnly costPerCharacter As Double
    Private ReadOnly costLimit As Double
    Private ReadOnly recoveryRate As Double

    Private availableSlack As Double = 0
    Private usedCost As Double = 0
    Private lastContinueConsumeTime As Date = DateTime.Now()
    Private consuming As Boolean

    Public Sub New(ByVal destinationStream As IO.Stream,
                   Optional ByVal initialSlack As Double = 0,
                   Optional ByVal costPerWrite As Double = 0,
                   Optional ByVal costPerCharacter As Double = 0,
                   Optional ByVal costLimit As Double = 0,
                   Optional ByVal costRecoveredPerSecond As Double = 1)
        Contract.Requires(destinationStream IsNot Nothing)
        Contract.Requires(initialSlack >= 0)
        Contract.Requires(costPerWrite >= 0)
        Contract.Requires(costPerCharacter >= 0)
        Contract.Requires(costLimit >= 0)
        Contract.Requires(costRecoveredPerSecond > 0)

        Me.destinationStream = destinationStream
        Me.availableSlack = initialSlack
        Me.costPerWrite = costPerWrite
        Me.costPerCharacter = costPerCharacter
        Me.costLimit = costLimit
        Me.recoveryRate = costRecoveredPerSecond / TimeSpan.TicksPerSecond
    End Sub

    Public Sub QueueWrite(ByVal data As Byte())
        EnqueueConsume(data)
    End Sub

    Protected Overrides Sub StartRunning()
        ref.QueueAction(Sub() Run())
    End Sub
    Protected Overrides Sub Consume(ByVal item As Byte())
        ref.QueueAction(Sub() ContinueConsume(item))
    End Sub
    Private Sub ContinueConsume(ByVal item As Byte())
        If item IsNot Nothing Then consumptionQueue.Enqueue(item)
        If consuming Then Return
        consuming = True

        While consumptionQueue.Count > 0
            'Recover
            Dim d = DateTime.Now()
            usedCost -= (d - lastContinueConsumeTime).Ticks * recoveryRate
            lastContinueConsumeTime = d
            If usedCost < 0 Then usedCost = 0

            'Wait
            If usedCost > costLimit + availableSlack Then
                FutureWait(New TimeSpan(CLng((usedCost - costLimit) / recoveryRate))).
                                CallWhenReady(Sub() ref.QueueAction(Sub() ContinueConsume(Nothing)))
                Return
            End If

            'Cut into slack
            If availableSlack > 0 Then
                Dim x = Math.Min(usedCost, availableSlack)
                availableSlack -= x
                usedCost -= x
            End If

            'Perform write
            Dim data = consumptionQueue.Dequeue()
            usedCost += costPerWrite + costPerCharacter * data.Length
            destinationStream.Write(data, 0, data.Length)
        End While

        consuming = False
    End Sub
End Class