﻿Imports Tinker.Pickling

Namespace WC3.Replay
    Public Module Extensions
        <DebuggerDisplay("{ToString}")>
        Private Class StreamAsList
            Inherits FutureDisposable
            Implements IReadableList(Of Byte)

            Private ReadOnly _stream As IRandomReadableStream
            Private ReadOnly _offset As Long
            Private ReadOnly _takeOwnershipofStream As Boolean

            <ContractInvariantMethod()> Private Sub ObjectInvariant()
                Contract.Invariant(_stream IsNot Nothing)
                Contract.Invariant(_offset >= 0)
                Contract.Invariant(_offset <= _stream.Length)
            End Sub

            Public Sub New(ByVal stream As IRandomReadableStream,
                           ByVal offset As Long,
                           ByVal takeOwnershipOfStream As Boolean)
                Contract.Requires(stream IsNot Nothing)
                Contract.Requires(offset >= 0)
                Contract.Requires(offset <= stream.Length)
                Me._stream = stream
                Me._offset = offset
                Me._takeOwnershipofStream = takeOwnershipOfStream
            End Sub

            <ContractVerification(False)>
            Public Function Contains(ByVal item As Byte) As Boolean Implements IReadableCollection(Of Byte).Contains
                Return (From e In Me Where item = e).Any
            End Function

            Public ReadOnly Property Count As Integer Implements IReadableCollection(Of Byte).Count
                Get
                    Return CInt(_stream.Length - _offset)
                End Get
            End Property

            <ContractVerification(False)>
            Public Function IndexOf(ByVal item As Byte) As Integer Implements IReadableList(Of Byte).IndexOf
                Return (From i In Enumerable.Range(0, Count)
                        Where Me(i) = item
                        Select i + 1
                        ).FirstOrDefault - 1
            End Function
            Default Public ReadOnly Property Item(ByVal index As Integer) As Byte Implements IReadableList(Of Byte).Item
                <ContractVerification(False)>
                Get
                    If Me.FutureDisposed.State <> FutureState.Unknown Then Throw New ObjectDisposedException(Me.GetType.FullName)
                    _stream.Position = _offset + index
                    Return _stream.ReadExact(exactCount:=1)(0)
                End Get
            End Property

            Public Function GetEnumerator() As IEnumerator(Of Byte) Implements IEnumerable(Of Byte).GetEnumerator
                Return (From i In Enumerable.Range(0, Count)
                        Select Item(i)).GetEnumerator
            End Function
            Private Function GetEnumeratorObj() As System.Collections.IEnumerator Implements System.Collections.IEnumerable.GetEnumerator
                Return GetEnumerator()
            End Function

            Public Overrides Function ToString() As String
                If Me.Count > 10 Then
                    Return "[{0}, ...".Frmt(Me.Take(10).StringJoin(", "))
                Else
                    Return "[{0}]".Frmt(Me.StringJoin(", "))
                End If
            End Function

            Protected Overrides Function PerformDispose(ByVal finalizing As Boolean) As IFuture
                If _takeOwnershipofStream Then _stream.Dispose()
                Return MyBase.PerformDispose(finalizing)
            End Function
        End Class

        <Extension()>
        <ContractVerification(False)>
        Public Function ReadPickle(Of T)(ByVal stream As IRandomReadableStream, ByVal jar As IJar(Of T)) As IPickle(Of T)
            Contract.Requires(stream IsNot Nothing)
            Contract.Requires(jar IsNot Nothing)
            Contract.Ensures(Contract.Result(Of IPickle(Of T))() IsNot Nothing)
            Contract.Ensures(stream.Position = Contract.OldValue(stream.Position) + Contract.Result(Of IPickle(Of T)).Data.Count)
            Dim oldPosition = stream.Position
            Using view = New StreamAsList(stream, oldPosition, takeOwnershipOfStream:=False)
                '[first parse: learn needed data]
                Dim result = jar.Parse(view)
                '[second parse: work with copied data, to avoid accessing the disposed StreamAsList later]
                result = jar.Parse(result.Data.ToArray.AsReadableList)
                'Place position after used data, as if it had been read normally
                stream.Position = oldPosition + result.Data.Count
                Return result
            End Using
        End Function

        <Extension()>
        <ContractVerification(False)>
        Public Function WritePickle(Of T)(ByVal stream As IO.Stream, ByVal jar As IPackJar(Of T), ByVal value As T) As IPickle(Of T)
            Contract.Requires(stream IsNot Nothing)
            Contract.Requires(jar IsNot Nothing)
            Contract.Requires(value IsNot Nothing)
            Contract.Ensures(Contract.Result(Of IPickle(Of T))() IsNot Nothing)
            Dim result = jar.Pack(value)
            stream.Write(result.Data.ToArray, 0, result.Data.Count)
            Return result
        End Function
    End Module
End Namespace
