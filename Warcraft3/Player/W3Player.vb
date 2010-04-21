﻿Imports Tinker.Pickling

Namespace WC3
    Public Enum HostTestResult As Integer
        Fail = -1
        Test = 0
        Pass = 1
    End Enum
    Public Enum PlayerState
        Lobby
        Loading
        Playing
    End Enum

    Partial Public NotInheritable Class Player
        Inherits DisposableWithTask
        Implements Download.IPlayerDownloadAspect

        Private state As PlayerState = PlayerState.Lobby
        Private ReadOnly _id As PlayerId
        Private ReadOnly testCanHost As Task
        Private ReadOnly socket As W3Socket
        Private ReadOnly packetHandler As Protocol.W3PacketHandler
        Private ReadOnly inQueue As CallQueue = New TaskedCallQueue
        Private ReadOnly outQueue As CallQueue = New TaskedCallQueue
        Private _numPeerConnections As Integer
        Private _downloadManager As Download.Manager
        Private ReadOnly pinger As Pinger

        Private ReadOnly _name As InvariantString
        Private ReadOnly _listenPort As UShort
        Public ReadOnly peerKey As UInteger
        Private ReadOnly _peerData As IReadableList(Of Byte)
        Public ReadOnly isFake As Boolean
        Private ReadOnly logger As Logger

        Public hasVotedToStart As Boolean
        Public adminAttemptCount As Integer
        Private _reportedDownloadPosition As UInt32? = Nothing

        Public Event Disconnected(ByVal sender As Player, ByVal expected As Boolean, ByVal reportedReason As Protocol.PlayerLeaveReason, ByVal reasonDescription As String)
        Public Event SuperficialStateUpdated(ByVal sender As Player)
        Public Event StateUpdated(ByVal sender As Player)

        <ContractInvariantMethod()> Private Sub ObjectInvariant()
            Contract.Invariant(_numPeerConnections >= 0)
            Contract.Invariant(_numPeerConnections <= 12)
            Contract.Invariant(tickQueue IsNot Nothing)
            Contract.Invariant(packetHandler IsNot Nothing)
            Contract.Invariant(logger IsNot Nothing)
            Contract.Invariant(inQueue IsNot Nothing)
            Contract.Invariant(_peerData IsNot Nothing)
            Contract.Invariant(outQueue IsNot Nothing)
            Contract.Invariant(socket Is Nothing = isFake)
            Contract.Invariant(testCanHost IsNot Nothing)
            Contract.Invariant(adminAttemptCount >= 0)
            Contract.Invariant(socket IsNot Nothing)
            Contract.Invariant(totalTockTime >= 0)
        End Sub

        '''<summary>Creates a fake player.</summary>
        Public Sub New(ByVal id As PlayerId,
                       ByVal name As InvariantString,
                       Optional ByVal logger As Logger = Nothing)
            If name.Length > Protocol.Packets.MaxPlayerNameLength Then Throw New ArgumentException("Player name must be less than 16 characters long.")
            Me.logger = If(logger, New Logger)
            Me.packetHandler = New Protocol.W3PacketHandler(name, Me.logger)
            Me._id = id
            Me._peerData = New Byte() {0}.AsReadableList
            Me._name = name
            isFake = True
            LobbyStart()
            Dim hostFail = New TaskCompletionSource(Of Boolean)
            hostFail.SetException(New ArgumentException("Fake players can't host."))
            Me.testCanHost = hostFail.Task
            Me.testCanHost.IgnoreExceptions()
        End Sub

        '''<summary>Creates a real player.</summary>
        Public Sub New(ByVal id As PlayerId,
                       ByVal connectingPlayer As W3ConnectingPlayer,
                       ByVal clock As IClock,
                       ByVal downloadManager As Download.Manager,
                       Optional ByVal logger As Logger = Nothing)
            'Contract.Requires(game IsNot Nothing)
            'Contract.Requires(connectingPlayer IsNot Nothing)
            Contract.Assume(connectingPlayer IsNot Nothing)

            Me.logger = If(logger, New Logger)
            Me.packetHandler = New Protocol.W3PacketHandler(connectingPlayer.Name, Me.logger)
            connectingPlayer.Socket.Logger = Me.logger
            Me.peerKey = connectingPlayer.PeerKey
            Me._peerData = connectingPlayer.PeerData

            Me._downloadManager = downloadManager
            Me.socket = connectingPlayer.Socket
            Me._name = connectingPlayer.Name
            Me._listenPort = connectingPlayer.ListenPort
            Me._id = id
            AddHandler socket.Disconnected, AddressOf OnSocketDisconnected

            AddRemotePacketHandler(Protocol.ClientPackets.Pong, Function(pickle)
                                                                    outQueue.QueueAction(Sub() RaiseEvent SuperficialStateUpdated(Me))
                                                                    Return pinger.QueueReceivedPong(pickle.Value)
                                                                End Function)
            AddQueuedLocalPacketHandler(Protocol.ClientPackets.NonGameAction, AddressOf ReceiveNonGameAction)
            AddQueuedLocalPacketHandler(Protocol.ClientPackets.Leaving, AddressOf ReceiveLeaving)
            AddQueuedLocalPacketHandler(Protocol.PeerPackets.MapFileDataReceived, AddressOf IgnorePacket)
            AddQueuedLocalPacketHandler(Protocol.PeerPackets.MapFileDataProblem, AddressOf IgnorePacket)

            LobbyStart()

            'Test hosting
            Me.testCanHost = AsyncTcpConnect(socket.RemoteEndPoint.Address, ListenPort)
            Me.testCanHost.IgnoreExceptions()

            'Pings
            pinger = New Pinger(period:=5.Seconds, timeoutCount:=10, clock:=clock)
            AddHandler pinger.SendPing, Sub(sender, salt) QueueSendPacket(Protocol.MakePing(salt))
            AddHandler pinger.Timeout, Sub(sender) QueueDisconnect(expected:=False,
                                                                   reportedReason:=Protocol.PlayerLeaveReason.Disconnect,
                                                                   reasonDescription:="Stopped responding to pings.")
        End Sub

        Public ReadOnly Property Name As InvariantString Implements Download.IPlayerDownloadAspect.Name
            Get
                Return _name
            End Get
        End Property
        Public ReadOnly Property Id As PlayerId Implements Download.IPlayerDownloadAspect.Id
            Get
                Return _id
            End Get
        End Property
        Public ReadOnly Property ListenPort As UShort
            Get
                Return _listenPort
            End Get
        End Property
        Public ReadOnly Property PeerData As IReadableList(Of Byte)
            Get
                Contract.Ensures(Contract.Result(Of IReadableList(Of Byte))() IsNot Nothing)
                Return _peerData
            End Get
        End Property
        Public ReadOnly Property CanHost() As HostTestResult
            Get
                Dim testState = testCanHost.Status
                Select Case testState
                    Case TaskStatus.Faulted : Return HostTestResult.Fail
                    Case TaskStatus.RanToCompletion : Return HostTestResult.Pass
                    Case Else : Return HostTestResult.Test
                End Select
            End Get
        End Property
        Public ReadOnly Property RemoteEndPoint As Net.IPEndPoint
            Get
                Contract.Ensures(Contract.Result(Of Net.IPEndPoint)() IsNot Nothing)
                Contract.Ensures(Contract.Result(Of Net.IPEndPoint)().Address IsNot Nothing)
                If isFake Then Return New Net.IPEndPoint(New Net.IPAddress({0, 0, 0, 0}), 0)
                Return socket.RemoteEndPoint
            End Get
        End Property

        Private Function AddQueuedLocalPacketHandler(Of T)(ByVal packetDefinition As Protocol.Packets.Definition(Of T),
                                                           ByVal handler As Action(Of IPickle(Of T))) As IDisposable
            Contract.Requires(packetDefinition IsNot Nothing)
            Contract.Requires(handler IsNot Nothing)
            Contract.Ensures(Contract.Result(Of IDisposable)() IsNot Nothing)
            packetHandler.AddLogger(packetDefinition.Id, packetDefinition.Jar)
            Return packetHandler.AddHandler(packetDefinition.Id, Function(data) inQueue.QueueAction(Sub() handler(packetDefinition.Jar.ParsePickle(data))))
        End Function

        Private Function AddRemotePacketHandler(Of T)(ByVal packetDefinition As Protocol.Packets.Definition(Of T),
                                                      ByVal handler As Func(Of IPickle(Of T), Task)) As IDisposable
            Contract.Requires(packetDefinition IsNot Nothing)
            Contract.Requires(handler IsNot Nothing)
            Contract.Ensures(Contract.Result(Of IDisposable)() IsNot Nothing)
            packetHandler.AddLogger(packetDefinition.Id, packetDefinition.Jar)
            Return packetHandler.AddHandler(packetDefinition.Id, Function(data) handler(packetDefinition.Jar.ParsePickle(data)))
        End Function
        Public Function QueueAddPacketHandler(Of T)(ByVal packetDefinition As Protocol.Packets.Definition(Of T),
                                                    ByVal handler As Func(Of IPickle(Of T), Task)) As Task(Of IDisposable) _
                                                    Implements Download.IPlayerDownloadAspect.QueueAddPacketHandler
            Return inQueue.QueueFunc(Function() AddRemotePacketHandler(packetDefinition, handler))
        End Function

        Public Sub QueueStart()
            inQueue.QueueAction(Sub() BeginReading())
        End Sub

        Private Sub BeginReading()
            AsyncProduceConsumeUntilError(
                producer:=AddressOf socket.AsyncReadPacket,
                consumer:=Function(packetData) packetHandler.HandlePacket(packetData),
                errorHandler:=Sub(exception) QueueDisconnect(expected:=False,
                                                             reportedReason:=Protocol.PlayerLeaveReason.Disconnect,
                                                             reasonDescription:="Error receiving packet: {0}.".Frmt(exception.Summarize)))
        End Sub

        '''<summary>Disconnects this player and removes them from the system.</summary>
        Private Sub Disconnect(ByVal expected As Boolean,
                               ByVal reportedReason As Protocol.PlayerLeaveReason,
                               ByVal reasonDescription As String)
            Contract.Requires(reasonDescription IsNot Nothing)
            If Not Me.isFake Then
                socket.Disconnect(expected, reasonDescription)
            End If
            If pinger IsNot Nothing Then pinger.Dispose()
            RaiseEvent Disconnected(Me, expected, reportedReason, reasonDescription)
            Me.Dispose()
        End Sub
        Public Function QueueDisconnect(ByVal expected As Boolean, ByVal reportedReason As Protocol.PlayerLeaveReason, ByVal reasonDescription As String) As Task Implements Download.IPlayerDownloadAspect.QueueDisconnect
            Return inQueue.QueueAction(Sub() Disconnect(expected, reportedReason, reasonDescription))
        End Function

        Private Sub SendPacket(ByVal pk As Protocol.Packet)
            Contract.Requires(pk IsNot Nothing)
            If Me.isFake Then Return
            socket.SendPacket(pk)
        End Sub
        Public Function QueueSendPacket(ByVal packet As Protocol.Packet) As Task Implements Download.IPlayerDownloadAspect.QueueSendPacket
            Dim result = inQueue.QueueAction(Sub() SendPacket(packet))
            result.IgnoreExceptions()
            Return result
        End Function

        Private Sub OnSocketDisconnected(ByVal sender As W3Socket, ByVal expected As Boolean, ByVal reasonDescription As String)
            inQueue.QueueAction(Sub() Disconnect(expected, Protocol.PlayerLeaveReason.Disconnect, reasonDescription))
        End Sub

        Public Function QueueGetLatency() As Task(Of Double)
            Contract.Ensures(Contract.Result(Of Task(Of Double))() IsNot Nothing)
            If pinger Is Nothing Then
                Return 0.0.AsTask
            Else
                Return pinger.QueueGetLatency
            End If
        End Function
        <ContractVerification(False)>
        Public Function QueueGetLatencyDescription() As Task(Of String)
            Contract.Ensures(Contract.Result(Of Task(Of String))() IsNot Nothing)
            Return (From latency In QueueGetLatency()
                    Select latencyDesc = If(latency = 0, "?", "{0:0}ms".Frmt(latency))
                    Select If(_downloadManager Is Nothing,
                              latencyDesc.AsTask,
                              _downloadManager.QueueGetClientLatencyDescription(Me, latencyDesc))
                   ).Unwrap.AssumeNotNull
        End Function
        Public ReadOnly Property PeerConnectionCount() As Integer
            Get
                Contract.Ensures(Contract.Result(Of Integer)() >= 0)
                Contract.Ensures(Contract.Result(Of Integer)() <= 12)
                Return _numPeerConnections
            End Get
        End Property

        Protected Overrides Function PerformDispose(ByVal finalizing As Boolean) As Task
            If finalizing Then Return Nothing
            Return QueueDisconnect(expected:=True, reportedReason:=Protocol.PlayerLeaveReason.Disconnect, reasonDescription:="Disposed")
        End Function

#Region "Lobby"
        <Pure()>
        <ContractVerification(False)>
        Public Function MakePacketOtherPlayerJoined() As Protocol.Packet Implements Download.IPlayerDownloadAspect.MakePacketOtherPlayerJoined
            Contract.Ensures(Contract.Result(Of Protocol.Packet)() IsNot Nothing)
            Return Protocol.MakeOtherPlayerJoined(Name, Id, peerKey, PeerData, New Net.IPEndPoint(RemoteEndPoint.Address, ListenPort))
        End Function

        Private Sub LobbyStart()
            state = PlayerState.Lobby
            AddQueuedLocalPacketHandler(Protocol.ClientPackets.PeerConnectionInfo, AddressOf OnReceivePeerConnectionInfo)
            AddQueuedLocalPacketHandler(Protocol.ClientPackets.ClientMapInfo, AddressOf OnReceiveClientMapInfo)
        End Sub

        Private Sub OnReceivePeerConnectionInfo(ByVal flags As IPickle(Of UInt16))
            Contract.Requires(flags IsNot Nothing)
            _numPeerConnections = (From i In 12.Range Where flags.Value.HasBitSet(i)).Count
            Contract.Assume(_numPeerConnections <= 12)
            RaiseEvent SuperficialStateUpdated(Me)
        End Sub
        Private Sub OnReceiveClientMapInfo(ByVal pickle As IPickle(Of NamedValueMap))
            Contract.Requires(pickle IsNot Nothing)
            _reportedDownloadPosition = pickle.Value.ItemAs(Of UInt32)("total downloaded")
            outQueue.QueueAction(Sub() RaiseEvent StateUpdated(Me))
        End Sub
#End Region

#Region "Load Screen"
        Private _ready As Boolean

        Public ReadOnly Property IsReady As Boolean
            Get
                Return isFake OrElse _ready
            End Get
        End Property

        Public Event ReceivedReady(ByVal sender As Player)
        Private Sub ReceiveReady(ByVal pickle As ISimplePickle)
            Contract.Requires(pickle IsNot Nothing)
            _ready = True
            logger.Log("{0} is ready".Frmt(Name), LogMessageType.Positive)
            outQueue.QueueAction(Sub() RaiseEvent ReceivedReady(Me))
        End Sub

        Private Sub StartLoading()
            state = PlayerState.Loading
            SendPacket(Protocol.MakeStartLoading())
            AddQueuedLocalPacketHandler(Protocol.ClientPackets.Ready, AddressOf ReceiveReady)
            AddQueuedLocalPacketHandler(Protocol.ClientPackets.GameAction, AddressOf ReceiveGameAction)
        End Sub
        Public Function QueueStartLoading() As Task
            Contract.Ensures(Contract.Result(Of Task)() IsNot Nothing)
            Return inQueue.QueueAction(AddressOf StartLoading)
        End Function
#End Region

#Region "GamePlay"
        Public Event ReceivedRequestDropLaggers(ByVal sender As Player)
        Public Event ReceivedGameActions(ByVal sender As Player, ByVal actions As IReadableList(Of Protocol.GameAction))

        Private ReadOnly tickQueue As New Queue(Of TickRecord)
        Private totalTockTime As Integer
        Private maxTockTime As Integer

        Public Sub GamePlayStart()
            state = PlayerState.Playing
            AddQueuedLocalPacketHandler(Protocol.ClientPackets.Tock, AddressOf ReceiveTock)
            AddQueuedLocalPacketHandler(Protocol.ClientPackets.RequestDropLaggers, AddressOf ReceiveRequestDropLaggers)
            AddQueuedLocalPacketHandler(Protocol.ClientPackets.ClientConfirmHostLeaving, Sub() SendPacket(Protocol.MakeHostConfirmHostLeaving()))
        End Sub

        Private Sub SendTick(ByVal record As TickRecord,
                             ByVal actionStreaks As IEnumerable(Of IReadableList(Of Protocol.PlayerActionSet)))
            Contract.Requires(actionStreaks IsNot Nothing)
            Contract.Requires(record IsNot Nothing)
            If isFake Then Return
            tickQueue.Enqueue(record)
            maxTockTime += record.length
            For Each preOverflowActionStreak In actionStreaks.SkipLast(1)
                Contract.Assume(preOverflowActionStreak IsNot Nothing)
                SendPacket(Protocol.MakeTickPreOverflow(preOverflowActionStreak))
            Next preOverflowActionStreak
            If actionStreaks.Any Then
                SendPacket(Protocol.MakeTick(record.length, actionStreaks.Last.AssumeNotNull.Maybe))
            Else
                SendPacket(Protocol.MakeTick(record.length))
            End If
        End Sub
        Public Function QueueSendTick(ByVal record As TickRecord,
                                      ByVal actionStreaks As IEnumerable(Of IReadableList(Of Protocol.PlayerActionSet))) As Task
            Contract.Requires(record IsNot Nothing)
            Contract.Requires(actionStreaks IsNot Nothing)
            Contract.Ensures(Contract.Result(Of Task)() IsNot Nothing)
            Return inQueue.QueueAction(Sub() SendTick(record, actionStreaks))
        End Function

        Private Sub ReceiveRequestDropLaggers(ByVal pickle As ISimplePickle)
            RaiseEvent ReceivedRequestDropLaggers(Me)
        End Sub

        Private Sub ReceiveGameAction(ByVal pickle As IPickle(Of IReadableList(Of Protocol.GameAction)))
            Contract.Requires(pickle IsNot Nothing)
            outQueue.QueueAction(Sub() RaiseEvent ReceivedGameActions(Me, pickle.Value))
        End Sub
        Private Sub ReceiveTock(ByVal pickle As IPickle(Of NamedValueMap))
            Contract.Requires(pickle IsNot Nothing)
            If tickQueue.Count <= 0 Then
                logger.Log("Banned behavior: {0} responded to a tick which wasn't sent.".Frmt(Name), LogMessageType.Problem)
                Disconnect(True, Protocol.PlayerLeaveReason.Disconnect, "overticked")
                Return
            End If

            Dim record = tickQueue.Dequeue()
            Contract.Assume(record IsNot Nothing)
            totalTockTime += record.length
            Contract.Assume(totalTockTime >= 0)
        End Sub

        Public ReadOnly Property GetTockTime() As Integer
            Get
                Contract.Ensures(Contract.Result(Of Integer)() >= 0)
                Return totalTockTime
            End Get
        End Property
        Public Function QueueStartPlaying() As Task
            Contract.Ensures(Contract.Result(Of Task)() IsNot Nothing)
            Return inQueue.QueueAction(AddressOf GamePlayStart)
        End Function
#End Region

#Region "Part"
        Public Event ReceivedNonGameAction(ByVal sender As Player, ByVal vals As NamedValueMap)

        Private Sub ReceiveNonGameAction(ByVal pickle As IPickle(Of NamedValueMap))
            Contract.Requires(pickle IsNot Nothing)
            RaiseEvent ReceivedNonGameAction(Me, pickle.Value)
        End Sub

        Private Sub IgnorePacket(ByVal pickle As IPickle(Of NamedValueMap))
        End Sub

        Private Sub ReceiveLeaving(ByVal pickle As IPickle(Of Protocol.PlayerLeaveReason))
            Contract.Requires(pickle IsNot Nothing)
            Dim leaveType = pickle.Value
            Disconnect(True, leaveType, "Controlled exit with reported result: {0}".Frmt(leaveType))
        End Sub

        Public ReadOnly Property AdvertisedDownloadPercent() As Byte
            Get
                If state <> PlayerState.Lobby Then Return 100
                If isFake OrElse _downloadManager Is Nothing Then Return 254 'Not a real player, show "|CF"
                If _reportedDownloadPosition Is Nothing Then Return 255
                Return CByte((_reportedDownloadPosition * 100UL) \ _downloadManager.FileSize)
            End Get
        End Property

        Public Function Description() As Task(Of String)
            Contract.Ensures(Contract.Result(Of Task(Of String))() IsNot Nothing)
            Return QueueGetLatencyDescription.Select(
                Function(latencyDesc)
                    Dim contextInfo As Task(Of String)
                    Select Case state
                        Case PlayerState.Lobby
                            Dim p = AdvertisedDownloadPercent
                            Dim dlText As String
                            Select Case p
                                Case 255 : dlText = "?"
                                Case 254 : dlText = "fake"
                                Case Else : dlText = "{0}%".Frmt(p)
                            End Select
                            contextInfo = From rateDescription In _downloadManager.QueueGetClientBandwidthDescription(Me)
                                          Select "DL={0}".Frmt(dlText).Padded(9) + _
                                                 "EB={0}".Frmt(rateDescription)
                        Case PlayerState.Loading
                            contextInfo = "Ready={0}".Frmt(IsReady).AsTask
                        Case PlayerState.Playing
                            contextInfo = "DT={0}gms".Frmt(Me.maxTockTime - Me.totalTockTime).AsTask
                        Case Else
                            Throw state.MakeImpossibleValueException
                    End Select
                    Contract.Assert(contextInfo IsNot Nothing)

                    Return From text In contextInfo
                           Select Name.Value.Padded(20) +
                                  Me.Id.ToString.Padded(6) +
                                  "Host={0}".Frmt(CanHost()).Padded(12) +
                                  "{0}c".Frmt(_numPeerConnections).Padded(5) +
                                  latencyDesc.Padded(12) +
                                  text
                End Function).Unwrap.AssumeNotNull
        End Function
#End Region
    End Class
End Namespace
