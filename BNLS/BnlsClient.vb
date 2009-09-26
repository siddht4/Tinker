﻿Imports System.Net
Imports System.Net.Sockets

Namespace BattleNetLogonServer
    Public Enum BnlsPacketId As Byte
        Null = &H0
        CdKey = &H1
        LogonChallenge = &H2
        LogonProof = &H3
        CreateAccount = &H4
        ChangeChallenge = &H5
        ChangeProof = &H6
        UpgradeChallenge = &H7
        UpgradeProof = &H8
        VersionCheck = &H9
        ConfirmLogon = &HA
        HashData = &HB
        CdKeyEx = &HC
        ChooseNlsRevision = &HD
        Authorize = &HE
        AuthorizeProof = &HF
        RequestVersionByte = &H10
        VerifyServer = &H11
        ReserveServerSlots = &H12
        ServerLogonChallenge = &H13
        ServerLogonProof = &H14
        Reserved0 = &H15
        Reserved1 = &H16
        Reserved2 = &H17
        VersionCheckEx = &H18
        Reserved3 = &H19
        VersionCheckEx2 = &H1A
        Warden = &H7D
    End Enum

    Public Class BnlsClient
        Implements IDisposable

        Private WithEvents socket As PacketSocket
        Public ReadOnly logger As Logger
        Private ReadOnly cookie As UInteger
        Public Event Send(ByVal data As Byte())
        Public Event Fail(ByVal e As Exception)
        Public Event Disconnect(ByVal sender As BnlsClient, ByVal expected As Boolean, ByVal reason As String)

        Private Sub New(ByVal socket As PacketSocket,
                        ByVal cookie As UInteger,
                        Optional ByVal logger As Logger = Nothing)
            logger = If(logger, New Logger())
            Me.logger = logger
            Me.socket = socket
            Me.cookie = cookie
            BeginReading()
        End Sub

        '''<summary>Asynchronously reads packets until an exception occurs, raising events to the outside.</summary>
        Private Sub BeginReading()
            'Read packets until an exception occurs
            Dim readLoop = FutureIterateExcept(Function() FutureReadBnlsPacket(socket, logger),
                Sub(packet)
                    'Check packet type
                    If packet.id <> BnlsWardenPacketId.FullServiceHandleWardenPacket Then
                        Throw New IO.IOException("Incorrect packet type received from server.")
                    End If

                    'Check packet contents
                    Dim vals = CType(packet.payload, IPickle(Of Dictionary(Of String, Object))).Value
                    If CUInt(vals("cookie")) <> cookie Then
                        Throw New IO.IOException("Incorrect cookie from server.")
                    ElseIf CUInt(vals("result")) <> 0 Then
                        Throw New IO.IOException("Server indicated there was a failure.")
                    End If

                    'Pass warden data out (and continue reading)
                    RaiseEvent Send(CType(vals("data"), Byte()))
                End Sub
            )

            'Pass exception out when readLoop fails
            readLoop.CallWhenReady(
                Sub(exception)
                    If exception IsNot Nothing Then
                        RaiseEvent Fail(exception)
                    End If
                End Sub
            )
        End Sub

        ''' <summary>Asynchronously connects to a BNLS server.</summary>
        Public Shared Function FutureConnectToBnlsServer(ByVal hostname As String,
                                                         ByVal port As UShort,
                                                         ByVal seed As UInteger,
                                                         Optional ByVal logger As Logger = Nothing) As IFuture(Of BnlsClient)
            Contract.Requires(hostname IsNot Nothing)
            Contract.Ensures(Contract.Result(Of IFuture(Of BnlsClient))() IsNot Nothing)
            logger = If(logger, New Logger())
            Dim cookie = seed

            'Connect and send first packet
            Dim futurePacketSocket = FutureCreateConnectedTcpClient(hostname, port).Select(
                Function(tcpClient)
                    Dim socket = New PacketSocket(tcpClient,
                                                  timeout:=5.Minutes,
                                                  numByteBeforeSize:=0,
                                                  numSizeBytes:=2,
                                                  logger:=logger,
                                                  name:="BNLS")
                    WritePacket(socket, logger, BnlsWardenPacket.MakeFullServiceConnect(seed, seed))
                    Return socket
                End Function
            )

            'Read response packet
            Dim futureReadPacket = futurePacketSocket.Select(
                Function(packetSocket) FutureReadBnlsPacket(packetSocket, logger)
            ).Defuturized

            'Process response packet and construct bnls client on success
            Dim futureBnlsClient = futureReadPacket.Select(
                Function(packet)
                    Dim packetSocket = futurePacketSocket.Value

                    'Check packet type
                    If packet.id <> BnlsWardenPacketId.FullServiceConnect Then
                        Dim msg = "Bnls server responded with a non-FullServiceConnect packet."
                        packetSocket.Disconnect(expected:=False, reason:=msg)
                        Throw New IO.IOException(msg)
                    End If

                    'Check packet contents
                    Dim vals = CType(packet.payload, IPickle(Of Dictionary(Of String, Object))).Value
                    If CUInt(vals("cookie")) <> cookie Then
                        Dim msg = "Incorrect cookie from server."
                        packetSocket.Disconnect(expected:=False, reason:=msg)
                        Throw New IO.IOException(msg)
                    End If

                    Return New BnlsClient(packetSocket, cookie, logger)
                End Function
            )

            Return futureBnlsClient
        End Function

        Private Shared Sub WritePacket(ByVal socket As PacketSocket,
                                       ByVal logger As Logger,
                                       ByVal pk As BnlsWardenPacket)
            Contract.Requires(pk IsNot Nothing)

            Try
                logger.Log(Function() "Sending {0} to {1}".Frmt(pk.id, socket.Name), LogMessageType.DataEvent)
                logger.Log(pk.payload.Description, LogMessageType.DataParsed)
                socket.WritePacket(Concat(Of Byte)({0, 0, BnlsPacketId.Warden, pk.id}, pk.payload.Data.ToArray))

            Catch e As Exception
                If Not (TypeOf e Is SocketException OrElse
                        TypeOf e Is ObjectDisposedException OrElse
                        TypeOf e Is IO.IOException OrElse
                        TypeOf e Is PicklingException) Then
                    LogUnexpectedException("Error sending {0} to {1}.".Frmt(pk.id, socket.Name), e)
                End If
                socket.Disconnect(expected:=False, reason:="Error sending {0} for {1}: {2}".Frmt(pk.id, socket.Name, e))
                Throw
            End Try
        End Sub

        Private Shared Function FutureReadBnlsPacket(ByVal socket As PacketSocket,
                                                     ByVal logger As Logger) As IFuture(Of BnlsWardenPacket)
            Return socket.FutureReadPacket().Select(
                Function(packetData)
                    'Check packet
                    If packetData.Length < 3 Then
                        Throw New IO.IOException("Packet doesn't have a header.")
                    ElseIf packetData(2) <> BnlsPacketId.Warden Then
                        Throw New IO.IOException("Not a bnls warden packet.")
                    End If

                    'Parse, log, return
                    Dim pk = BnlsWardenPacket.FromServerData(packetData.SubView(3))
                    logger.Log(Function() "Received {0} from {1}".Frmt(pk.id, socket.Name), LogMessageType.DataEvent)
                    logger.Log(pk.payload.Description, LogMessageType.DataParsed)
                    Return pk
                End Function)
        End Function

        Public Sub ProcessWardenPacket(ByVal data As ViewableList(Of Byte))
            WritePacket(socket, logger, BnlsWardenPacket.MakeFullServiceHandleWardenPacket(cookie, data.ToArray))
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            socket.Disconnect(expected:=True, reason:="Disposed")
            GC.SuppressFinalize(Me)
        End Sub

        Private Sub OnSocketDisconnect(ByVal sender As PacketSocket, ByVal expected As Boolean, ByVal reason As String) Handles socket.Disconnected
            RaiseEvent Disconnect(Me, expected, reason)
        End Sub
    End Class
End Namespace