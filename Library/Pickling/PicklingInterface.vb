﻿Namespace Pickling
    <Serializable()>
    Public Class PicklingException
        Inherits Exception
        Public Sub New(Optional ByVal message As String = Nothing,
                       Optional ByVal innerException As Exception = Nothing)
            MyBase.New(message, innerException)
        End Sub
    End Class

    <ContractClass(GetType(ContractClassIPickle))>
    Public Interface IPickle
        ReadOnly Property Data As ViewableList(Of Byte)
        ReadOnly Property Description As LazyValue(Of String)
    End Interface

    <ContractClass(GetType(ContractClassIPickle(Of )))>
    Public Interface IPickle(Of Out T)
        Inherits IPickle
        ReadOnly Property Value As T
    End Interface

    <ContractClass(GetType(ContractClassForIJarInfo))>
    Public Interface IJarInfo
        ReadOnly Property Name As String
    End Interface
    <ContractClass(GetType(ContractClassIPackJar(Of )))>
    Public Interface IPackJar(Of In T)
        Inherits IJarInfo
        Function Pack(Of TValue As T)(ByVal value As TValue) As IPickle(Of TValue)
    End Interface
    <ContractClass(GetType(ContractClassIParseJar(Of )))>
    Public Interface IParseJar(Of Out T)
        Inherits IJarInfo
        Function Parse(ByVal data As ViewableList(Of Byte)) As IPickle(Of T)
    End Interface
    Public Interface IJar(Of T)
        Inherits IJarInfo
        Inherits IPackJar(Of T)
        Inherits IParseJar(Of T)
    End Interface

    <ContractClassFor(GetType(IPickle))>
    Public Class ContractClassIPickle
        Implements IPickle
        Public ReadOnly Property Data As ViewableList(Of Byte) Implements IPickle.Data
            Get
                Contract.Ensures(Contract.Result(Of ViewableList(Of Byte))() IsNot Nothing)
                Throw New NotSupportedException
            End Get
        End Property
        Public ReadOnly Property Description As LazyValue(Of String) Implements IPickle.Description
            Get
                Contract.Ensures(Contract.Result(Of LazyValue(Of String))() IsNot Nothing)
                Throw New NotSupportedException
            End Get
        End Property
    End Class

    <ContractClassFor(GetType(IPickle(Of )))>
    Public Class ContractClassIPickle(Of T)
        Implements IPickle(Of T)
        Public ReadOnly Property Data As ViewableList(Of Byte) Implements IPickle.Data
            Get
                Throw New NotSupportedException
            End Get
        End Property
        Public ReadOnly Property Description As LazyValue(Of String) Implements IPickle.Description
            Get
                Throw New NotSupportedException
            End Get
        End Property
        Public ReadOnly Property Value As T Implements IPickle(Of T).Value
            Get
                Throw New NotSupportedException
            End Get
        End Property
    End Class

    <ContractClassFor(GetType(IJarInfo))>
    Public Class ContractClassForIJarInfo
        Implements IJarInfo
        Public ReadOnly Property Name As String Implements IJarInfo.Name
            Get
                Contract.Ensures(Contract.Result(Of String)() IsNot Nothing)
                Throw New NotSupportedException()
            End Get
        End Property
    End Class

    <ContractClassFor(GetType(IPackJar(Of )))>
    Public Class ContractClassIPackJar(Of T)
        Implements IPackJar(Of T)
        Public ReadOnly Property Name As String Implements IJarInfo.Name
            Get
                Throw New NotSupportedException()
            End Get
        End Property
        Public Function Pack(Of TValue As T)(ByVal value As TValue) As IPickle(Of TValue) Implements IPackJar(Of T).Pack
            Contract.Ensures(Contract.Result(Of IPickle(Of TValue))() IsNot Nothing)
            Throw New NotSupportedException()
        End Function
    End Class

    <ContractClassFor(GetType(IParseJar(Of )))>
    Public Class ContractClassIParseJar(Of T)
        Implements IParseJar(Of T)
        Public ReadOnly Property Name As String Implements IJarInfo.Name
            Get
                Throw New NotSupportedException()
            End Get
        End Property
        Public Function Parse(ByVal data As ViewableList(Of Byte)) As IPickle(Of T) Implements IParseJar(Of T).Parse
            Contract.Requires(data IsNot Nothing)
            Contract.Ensures(Contract.Result(Of IPickle(Of T))() IsNot Nothing)
            Throw New NotSupportedException()
        End Function
    End Class
End Namespace