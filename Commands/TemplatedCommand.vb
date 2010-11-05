﻿Namespace Commands
    ''' <summary>
    ''' A command which processes arguments matching a template.
    ''' </summary>
    <ContractClass(GetType(ContractClassTemplatedCommand(Of )))>
    Public MustInherit Class TemplatedCommand(Of TTarget)
        Inherits BaseCommand(Of TTarget)

        Private ReadOnly _template As CommandTemplate

        <ContractInvariantMethod()> Private Sub ObjectInvariant()
            Contract.Invariant(_template IsNot Nothing)
        End Sub

        Protected Sub New(ByVal name As InvariantString,
                          ByVal template As InvariantString,
                          ByVal description As String,
                          Optional ByVal permissions As String = Nothing,
                          Optional ByVal extraHelp As String = Nothing,
                          Optional ByVal hasPrivateArguments As Boolean = False)
            MyBase.New(name, template, description, permissions, extraHelp, hasPrivateArguments)
            Contract.Requires(description IsNot Nothing)
            Me._template = New CommandTemplate(template)
        End Sub

        Protected NotOverridable Overloads Overrides Function PerformInvoke(ByVal target As TTarget, ByVal user As BotUser, ByVal argument As String) As Task(Of String)
            Dim arg = New CommandArgument(argument)
            Dim argException = _template.TryFindMismatch(arg)
            If argException IsNot Nothing Then Throw argException
            Return PerformInvoke(target, user, arg)
        End Function

        '''<summary>Uses a parsed argument to processes the command.</summary>
        Protected MustOverride Overloads Function PerformInvoke(ByVal target As TTarget, ByVal user As BotUser, ByVal argument As CommandArgument) As Task(Of String)
    End Class
    <ContractClassFor(GetType(TemplatedCommand(Of )))>
    Public MustInherit Class ContractClassTemplatedCommand(Of TTarget)
        Inherits TemplatedCommand(Of TTarget)
        Protected Sub New()
            MyBase.New("", "", "")
        End Sub
        Protected Overrides Function PerformInvoke(ByVal target As TTarget, ByVal user As BotUser, ByVal argument As CommandArgument) As Task(Of String)
            Contract.Requires(target IsNot Nothing)
            Contract.Requires(argument IsNot Nothing)
            Contract.Ensures(Contract.Result(Of Task(Of String))() IsNot Nothing)
            Throw New NotSupportedException
        End Function
    End Class
End Namespace
