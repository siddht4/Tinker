Namespace Commands
    ''' <summary>
    ''' A parsed case-insensitive text command argument.
    ''' </summary>
    <DebuggerDisplay("{ToString}")>
    Public NotInheritable Class CommandArgument
        Private Shared ReadOnly Delimiters As New Dictionary(Of Char, Char) From {
            {"("c, ")"c},
            {"{"c, "}"c},
            {"<"c, ">"c},
            {"["c, "]"c}}

        Private ReadOnly _text As String
        Private ReadOnly _raw As New List(Of String)
        Private ReadOnly _optionalSwitches As New HashSet(Of String)
        Private ReadOnly _named As New Dictionary(Of String, String)
        Private ReadOnly _optionalNamed As New Dictionary(Of String, String)

        <ContractInvariantMethod()> Private Sub ObjectInvariant()
            Contract.Invariant(_raw IsNot Nothing)
            Contract.Invariant(_optionalSwitches IsNot Nothing)
            Contract.Invariant(_named IsNot Nothing)
            Contract.Invariant(_optionalNamed IsNot Nothing)
            Contract.Invariant(_text IsNot Nothing)
        End Sub

        ''' <summary>
        ''' Constructs the command argument by parsing the given text.
        ''' </summary>
        Public Sub New(ByVal text As String)
            Contract.Requires(text IsNot Nothing)
            Me._text = text

            For Each word In Tokenize(text)
                Dim isNamed = word.Contains("="c)
                Dim isOptional = word.StartsWith("-"c)
                If isOptional Then word = word.Substring(1)

                If isNamed Then
                    Dim p = word.IndexOf("="c)
                    Dim name = word.Substring(0, p)
                    Dim value = word.Substring(p + 1)
                    If isOptional Then
                        If _optionalNamed.ContainsKey(name.ToUpperInvariant) Then
                            Throw New ArgumentException("The optional named argument '{0}' is specified twice.".Frmt(name))
                        End If
                        _optionalNamed.Add(name.ToUpperInvariant, value)
                    Else
                        If _named.ContainsKey(name.ToUpperInvariant) Then
                            Throw New ArgumentException("The named argument '{0}' is specified twice.".Frmt(name))
                        End If
                        _named.Add(name.ToUpperInvariant, value)
                    End If
                Else
                    If isOptional Then
                        If _optionalSwitches.Contains(word.ToUpperInvariant) Then
                            Throw New ArgumentException("The optional switch '{0}' is specified twice.".Frmt(word))
                        End If
                        _optionalSwitches.Add(word.ToUpperInvariant)
                    Else
                        _raw.Add(word)
                    End If
                End If
            Next word
        End Sub

        ''' <summary>
        ''' Splits text into tokens separated by spaces and fused by brackets.
        ''' </summary>
        Public Shared Function Tokenize(ByVal text As String) As IEnumerable(Of String)
            Contract.Requires(text IsNot Nothing)
            Contract.Ensures(Contract.Result(Of IEnumerable(Of String))() IsNot Nothing)

            Dim result = New List(Of String)
            Dim fusingTokens As Boolean
            Dim fusedTokens = New List(Of String)
            Dim e As Char

            For Each word In text.Split(" "c)
                If fusingTokens Then
                    'building delimited value
                    If word.EndsWith(e) Then
                        fusedTokens.Add(word.Substring(0, word.Length - 1))
                        result.Add(fusedTokens.StringJoin(" "))
                        fusedTokens.Clear()
                        fusingTokens = False
                    Else
                        fusedTokens.Add(word)
                    End If
                ElseIf word.Length <= 0 Then
                    'ignore; just a double-spaced argument separation
                ElseIf Delimiters.TryGetValue(word(0), e) Then
                    'delimited raw value
                    If word.EndsWith(e) Then
                        result.Add(word.Substring(1, word.Length - 2))
                    Else
                        fusingTokens = True
                        fusedTokens.Add(word.Substring(1))
                    End If
                ElseIf word.Contains("="c) Then
                    'named value
                    Dim j = word.IndexOf("="c) + 1
                    If j < word.Length AndAlso Delimiters.TryGetValue(word(j), e) Then
                        'named delimited value
                        If word.EndsWith(e) Then
                            result.Add(word.Substring(0, j) + word.Substring(j + 1, word.Length - j - 2))
                        Else
                            fusingTokens = True
                            fusedTokens.Add(word.Substring(0, j) + word.Substring(j + 1))
                        End If
                    Else
                        result.Add(word)
                    End If
                Else
                    'raw or optional value
                    result.Add(word)
                End If
            Next word

            If fusingTokens Then
                Throw New ArgumentException("A delimited value was not closed. Expected a matching '{0}' at the end of a word.".Frmt(e))
            End If

            Return result
        End Function

        '''<summary>Enumerates the raw values in the argument.</summary>
        Public ReadOnly Property RawValues As IEnumerable(Of String)
            Get
                Contract.Ensures(Contract.Result(Of IEnumerable(Of String))() IsNot Nothing)
                Return _raw
            End Get
        End Property
        '''<summary>Enumerates the optional switches in the argument.</summary>
        Public ReadOnly Property Switches As IEnumerable(Of String)
            Get
                Contract.Ensures(Contract.Result(Of IEnumerable(Of String))() IsNot Nothing)
                Return _optionalSwitches
            End Get
        End Property
        '''<summary>Enumerates the names of named values in the argument.</summary>
        Public ReadOnly Property Names As IEnumerable(Of String)
            Get
                Contract.Ensures(Contract.Result(Of IEnumerable(Of String))() IsNot Nothing)
                Return _named.Keys
            End Get
        End Property
        '''<summary>Enumerates the names of optional named values in the argument.</summary>
        Public ReadOnly Property OptionalNames As IEnumerable(Of String)
            Get
                Contract.Ensures(Contract.Result(Of IEnumerable(Of String))() IsNot Nothing)
                Return _optionalNamed.Keys
            End Get
        End Property
        '''<summary>Returns the number of raw values in the argument.</summary>
        Public ReadOnly Property RawValueCount As Integer
            Get
                Contract.Ensures(Contract.Result(Of Integer)() >= 0)
                Return _raw.Count
            End Get
        End Property
        '''<summary>Returns the total number of tokens in the argument (raw values, optional switches, named values, and optional named values).</summary>
        Public ReadOnly Property Count As Integer
            Get
                Contract.Ensures(Contract.Result(Of Integer)() >= 0)
                Return _raw.Count + _optionalSwitches.Count + _named.Count + _optionalNamed.Count
            End Get
        End Property

        '''<summary>Returns the specified raw value from the argument.</summary>
        Public ReadOnly Property RawValue(ByVal index As Integer) As String
            Get
                Contract.Requires(index >= 0)
                Contract.Ensures(Contract.Result(Of String)() IsNot Nothing)
                If index >= _raw.Count Then Throw New ArgumentOutOfRangeException("index", "index is past the number of raw arguments.")
                Return _raw(index)
            End Get
        End Property
        '''<summary>Returns the specified raw value from the argument, or returns nothing if the raw value is not specified.</summary>
        Public ReadOnly Property TryGetRawValue(ByVal index As Integer) As String
            Get
                Contract.Requires(index >= 0)
                If index >= _raw.Count Then Return Nothing
                Return _raw(index)
            End Get
        End Property
        '''<summary>Returns the value of the specified named value from the argument.</summary>
        Public ReadOnly Property NamedValue(ByVal name As String) As String
            Get
                Contract.Requires(name IsNot Nothing)
                Contract.Ensures(Contract.Result(Of String)() IsNot Nothing)
                If Not _named.ContainsKey(name.ToUpperInvariant) Then Throw New InvalidOperationException("No such named argument.")
                Return _named(name.ToUpperInvariant)
            End Get
        End Property
        ''' <summary>Determines if the argument contains the given optional switch.</summary>
        ''' <param name="switch">The optional switch to check for. Do not include the leading '-'.</param>
        Public ReadOnly Property HasOptionalSwitch(ByVal switch As String) As Boolean
            Get
                Contract.Requires(switch IsNot Nothing)
                Return _optionalSwitches.Contains(switch.ToUpperInvariant)
            End Get
        End Property
        ''' <summary>
        ''' Returns the value of the specified named value from the argument.
        ''' Returns nothing if the named value is not included in the argument.
        ''' </summary>
        Public ReadOnly Property TryGetNamedValue(ByVal name As String) As String
            Get
                Contract.Requires(name IsNot Nothing)
                If Not _named.ContainsKey(name.ToUpperInvariant) Then Return Nothing
                Return _named(name.ToUpperInvariant)
            End Get
        End Property
        ''' <summary>
        ''' Returns the value of the specified optional named value from the argument.
        ''' Returns nothing if the optional named value is not included in the argument.
        ''' </summary>
        ''' <param name="name">The name of the optional value to check for. Do not include the leading '-'.</param>
        Public ReadOnly Property TryGetOptionalNamedValue(ByVal name As String) As String
            Get
                Contract.Requires(name IsNot Nothing)
                If Not _optionalNamed.ContainsKey(name.ToUpperInvariant) Then Return Nothing
                Return _optionalNamed(name.ToUpperInvariant)
            End Get
        End Property

        Public Overrides Function ToString() As String
            Return _text
        End Function
    End Class
End Namespace
