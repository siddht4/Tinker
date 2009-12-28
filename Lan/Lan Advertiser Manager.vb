﻿Imports Tinker.Components
Imports Tinker.Bot

Namespace Lan
    Public Class AdvertiserManager
        Inherits FutureDisposable
        Implements IBotComponent

        Public Shared ReadOnly LanCommands As New Lan.AdvertiserCommands()

        Private ReadOnly inQueue As ICallQueue = New TaskedCallQueue
        Private ReadOnly _name As InvariantString
        Private ReadOnly _bot As Bot.MainBot
        Private ReadOnly _advertiser As Lan.Advertiser
        Private ReadOnly _hooks As New List(Of IFuture(Of IDisposable))
        Private ReadOnly _control As Control

        <ContractInvariantMethod()> Private Sub ObjectInvariant()
            Contract.Invariant(inQueue IsNot Nothing)
            Contract.Invariant(_advertiser IsNot Nothing)
            Contract.Invariant(_hooks IsNot Nothing)
            Contract.Invariant(_bot IsNot Nothing)
            Contract.Invariant(_control IsNot Nothing)
        End Sub

        Public Sub New(ByVal name As InvariantString,
                       ByVal bot As Bot.MainBot,
                       ByVal advertiser As Lan.Advertiser)
            Contract.Requires(advertiser IsNot Nothing)
            Contract.Requires(bot IsNot Nothing)

            Me._bot = bot
            Me._name = name
            Me._advertiser = advertiser
            Me._control = New LanAdvertiserControl(Me)
        End Sub

        Public ReadOnly Property Name As InvariantString Implements IBotComponent.Name
            Get
                Return _name
            End Get
        End Property
        Public ReadOnly Property Type As InvariantString Implements IBotComponent.Type
            Get
                Return "Lan"
            End Get
        End Property
        Public ReadOnly Property HasControl As Boolean Implements IBotComponent.HasControl
            Get
                Contract.Ensures(Contract.Result(Of Boolean)())
                Return True
            End Get
        End Property
        Public ReadOnly Property Control As Control Implements IBotComponent.Control
            Get
                Return _control
            End Get
        End Property
        Public ReadOnly Property Advertiser As Lan.Advertiser
            Get
                Contract.Ensures(Contract.Result(Of Lan.Advertiser)() IsNot Nothing)
                Return _advertiser
            End Get
        End Property
        Public ReadOnly Property Bot As Bot.MainBot
            Get
                Contract.Ensures(Contract.Result(Of Bot.MainBot)() IsNot Nothing)
                Return _bot
            End Get
        End Property

        Public Function InvokeCommand(ByVal user As BotUser, ByVal argument As String) As IFuture(Of String) Implements IBotComponent.InvokeCommand
            Return LanCommands.Invoke(Me, user, argument)
        End Function

        Public Function IsArgumentPrivate(ByVal argument As String) As Boolean Implements IBotComponent.IsArgumentPrivate
            Return LanCommands.IsArgumentPrivate(argument)
        End Function

        Public ReadOnly Property Logger As Logger Implements IBotComponent.Logger
            Get
                Return _advertiser.Logger
            End Get
        End Property

        Protected Overrides Function PerformDispose(ByVal finalizing As Boolean) As Strilbrary.Threading.IFuture
            For Each hook In _hooks
                Contract.Assume(hook IsNot Nothing)
                hook.CallOnValueSuccess(Sub(value) value.Dispose()).SetHandled()
            Next hook
            _advertiser.Dispose()
            _control.Dispose()
            QueueSetAutomatic(False)
            Return Nothing
        End Function

        Private _autoHook As IFuture(Of IDisposable)
        Private Sub SetAutomatic(ByVal slaved As Boolean)
            If slaved = (_autoHook IsNot Nothing) Then Return
            If slaved Then
                _autoHook = _bot.QueueCreateActiveGameSetsAsyncView(
                        adder:=Sub(sender, server, gameSet) _advertiser.QueueAddGame(gameSet.GameSettings.GameDescription).SetHandled(),
                        remover:=Sub(sender, server, gameSet) _advertiser.QueueRemoveGame(gameSet.GameSettings.GameDescription.GameId).SetHandled())
            Else
                _autoHook.CallOnValueSuccess(Sub(value) value.Dispose()).SetHandled()
                _autoHook = Nothing
            End If
        End Sub
        Public Function QueueSetAutomatic(ByVal slaved As Boolean) As IFuture
            Contract.Ensures(Contract.Result(Of ifuture)() IsNot Nothing)
            Return inQueue.QueueAction(Sub() SetAutomatic(slaved))
        End Function
    End Class
End Namespace