﻿Namespace WC3
    Public NotInheritable Class W3ServerDoor
        Public ReadOnly server As GameServer
        Public ReadOnly logger As Logger
        Private WithEvents _accepter As W3ConnectionAccepter
        Private connectingPlayers As New List(Of W3ConnectingPlayer)
        Private ReadOnly lock As New Object()

        Public Sub New(ByVal server As GameServer,
                       Optional ByVal logger As Logger = Nothing)
            'contract bug wrt interface event implementation requires this:
            'Contract.Requires(server IsNot Nothing)
            Contract.Assume(server IsNot Nothing)
            Me.logger = If(logger, New Logger())
            Me.server = server
            Me._accepter = New W3ConnectionAccepter(Me.logger)
        End Sub

        Public ReadOnly Property Accepter() As W3ConnectionAccepter
            Get
                Return _accepter
            End Get
        End Property

        ''' <summary>
        ''' Clears pending connections and stops listening on all ports.
        ''' WARNING: Doesn't guarantee no more players entering the server!
        ''' For example find_game_for_player might be half-finished, resulting in a player joining a game after the reset.
        ''' </summary>
        Public Sub Reset()
            SyncLock lock
                Accepter.Reset()
                For Each player In connectingPlayers
                    player.Socket.Disconnect(expected:=True, reason:="Reset Server Door")
                Next player
                connectingPlayers.Clear()
            End SyncLock
        End Sub

        Private Sub OnConnection(ByVal sender As W3ConnectionAccepter,
                                 ByVal player As W3ConnectingPlayer) Handles _accepter.Connection
            SyncLock lock
                connectingPlayers.Add(player)
            End SyncLock
            FindGameForPlayer(player)
        End Sub

        Private Sub FindGameForPlayer(ByVal player As W3ConnectingPlayer)
            Dim addedPlayerFilter = Function(game As Game)
                                        Return game.QueueTryAddPlayer(player).EvalWhenValueReady(
                                            Function(addedPlayer, playerException) addedPlayer IsNot Nothing
                                        )
                                            End Function

            Dim futureSelectedGame = server.QueueGetGames.Select(
                                                     Function(games) games.FutureSelect(addedPlayerFilter)).Defuturized()

            futureSelectedGame.CallWhenValueReady(
                Sub(game, gameException)
                    If server.settings.instances = 0 AndAlso gameException IsNot Nothing Then
                        server.QueueCreateGame.CallWhenReady(
                            Sub(createdException)
                                If createdException Is Nothing Then
                                    FindGameForPlayer(player)
                                Else
                                    FailConnectingPlayer(player, "Failed to create game for player: {0}".Frmt(createdException))
                                End If
                            End Sub
                        )
                                Else
                                    If gameException IsNot Nothing Then
                                        FailConnectingPlayer(player, "Failed to find game for player (eg. {0}).".Frmt(gameException.Message))
                                    Else
                                        SyncLock lock
                                            connectingPlayers.Remove(player)
                                        End SyncLock
                                        logger.Log("Player {0} entered game {1}.".Frmt(player.Name, game.Name), LogMessageType.Positive)
                                    End If
                                End If
                            End Sub
            )
        End Sub
        Private Sub FailConnectingPlayer(ByVal player As W3ConnectingPlayer, ByVal reason As String)
            Contract.Requires(player IsNot Nothing)
            Contract.Requires(reason IsNot Nothing)
            SyncLock lock
                connectingPlayers.Remove(player)
            End SyncLock
            logger.Log("Couldn't find a game for player {0}.".Frmt(player.Name), LogMessageType.Negative)
            player.Socket.SendPacket(Packet.MakeReject(Packet.RejectReason.GameFull))
            player.Socket.Disconnect(expected:=True, reason:=reason)
        End Sub
    End Class
End Namespace