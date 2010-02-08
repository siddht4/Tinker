﻿Imports Tinker.Pickling

Namespace WC3.Replay
    Public Class ReplayReader
        Private ReadOnly _version As UInt32
        Private ReadOnly _gameTimeLength As UInt32
        Private ReadOnly _blockCount As UInt32
        Private ReadOnly _firstBlockOffset As UInt32
        Private ReadOnly _dataSize As UInt32
        Private ReadOnly _streamFactory As Func(Of IRandomReadableStream)

        <ContractInvariantMethod()> Private Shadows Sub ObjectInvariant()
            Contract.Invariant(_streamFactory IsNot Nothing)
        End Sub

        Public Sub New(ByVal streamFactory As Func(Of IRandomReadableStream),
                       ByVal blockCount As UInt32,
                       ByVal version As UInt32,
                       ByVal gameTimeLength As UInt32,
                       ByVal firstBlockOffset As UInt32,
                       ByVal dataDecompressedSize As UInt32)
            Contract.Requires(streamFactory IsNot Nothing)
            Me._streamFactory = streamFactory
            Me._version = version
            Me._gameTimeLength = gameTimeLength
            Me._firstBlockOffset = firstBlockOffset
            Me._dataSize = dataDecompressedSize
            Me._blockCount = blockCount
        End Sub

        Public Shared Function FromFile(ByVal path As String) As ReplayReader
            Contract.Requires(path IsNot Nothing)
            Contract.Ensures(Contract.Result(Of ReplayReader)() IsNot Nothing)
            Return ReplayReader.FromStreamFactory(Function() New IO.FileStream(path, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.Read).AsRandomReadableStream)
        End Function
        <ContractVerification(False)>
        Public Shared Function FromStreamFactory(ByVal streamFactory As Func(Of IRandomReadableStream)) As ReplayReader
            Contract.Requires(streamFactory IsNot Nothing)
            Contract.Ensures(Contract.Result(Of ReplayReader)() IsNot Nothing)

            Using stream = streamFactory()
                If stream Is Nothing Then Throw New InvalidStateException("Invalid streamFactory")
                'Read header values
                Dim magic = stream.ReadNullTerminatedString(maxLength:=28)
                Dim headerSize = stream.ReadUInt32()
                Dim compressedSize = stream.ReadUInt32()
                Dim headerVersion = stream.ReadUInt32()
                Dim decompressedSize = stream.ReadUInt32()
                Dim blockCount = stream.ReadUInt32()
                Dim productId = stream.ReadExact(4).ParseChrString(nullTerminated:=False)
                Dim gameVersion = stream.ReadUInt32()
                Dim gameVersion2 = stream.ReadUInt16()
                Dim flags = stream.ReadUInt16()
                Dim lengthInMilliseconds = stream.ReadUInt32()
                Dim headerCRC32 = stream.ReadUInt32()
                Contract.Assume(stream.Position = Prots.HeaderSize)

                'Check header values
                If magic <> Prots.HeaderMagicValue Then Throw New IO.InvalidDataException("Not a wc3 replay (incorrect magic value).")
                If productId <> "PX3W" Then Throw New IO.InvalidDataException("Not a wc3 replay (incorrect product id).")
                If headerSize <> Prots.HeaderSize Then Throw New IO.InvalidDataException("Not a recognized wc3 replay (incorrect version).")
                If headerVersion <> Prots.HeaderVersion Then Throw New IO.InvalidDataException("Not a recognized wc3 replay (incorrect version).")

                'Check header checksum
                stream.Position = 0
                Dim actualChecksum = stream.ReadExact(CInt(headerSize - 4)).Concat({0, 0, 0, 0}).CRC32
                If actualChecksum <> headerCRC32 Then Throw New IO.InvalidDataException("Not a wc3 replay (incorrect checksum).")

                Return New ReplayReader(streamFactory:=streamFactory,
                                        blockCount:=blockCount,
                                        Version:=gameVersion,
                                        GameTimeLength:=lengthInMilliseconds,
                                        firstBlockOffset:=headerSize,
                                        dataDecompressedSize:=decompressedSize)
            End Using
        End Function

        Public ReadOnly Property GameTimeLength As TimeSpan
            Get
                Contract.Ensures(Contract.Result(Of TimeSpan)().Ticks >= 0)
                Return _gameTimeLength.Milliseconds
            End Get
        End Property
        Public ReadOnly Property Version As UInt32
            Get
                Return _version
            End Get
        End Property

        Public Function MakeDataStream() As IRandomReadableStream
            Contract.Ensures(Contract.Result(Of IRandomReadableStream)() IsNot Nothing)
            Dim stream = _streamFactory()
            If stream Is Nothing Then Throw New InvalidStateException("Invalid stream factory.")
            Return New ReplayDataReader(stream, _blockCount, _firstBlockOffset, _dataSize)
        End Function

        Private Shared Function ReadPlayerRecord(ByVal stream As IRandomReadableStream) As Dictionary(Of InvariantString, Object)
            Contract.Requires(stream IsNot Nothing)
            Contract.Ensures(Contract.Result(Of Dictionary(Of InvariantString, Object))() IsNot Nothing)
            Return stream.ReadPickle(Prots.PlayerRecord).Value
        End Function
        Private Shared Function ReadSlotRecord(ByVal stream As IRandomReadableStream) As Dictionary(Of InvariantString, Object)
            Contract.Requires(stream IsNot Nothing)
            Contract.Ensures(Contract.Result(Of Dictionary(Of InvariantString, Object))() IsNot Nothing)
            Return stream.ReadPickle(New WC3.Protocol.SlotJar("slot")).Value
        End Function

        Private Shared Function ReadHeaderPlayerRecords(ByVal stream As IRandomReadableStream) As IReadableList(Of Object)
            Dim result = New List(Of Object)
            Do
                Dim b = stream.ReadByte() '0 = host, &H16 = other, else keep going
                If b <> 0 AndAlso b <> &H16 Then Exit Do
                Dim player = ReadPlayerRecord(stream)
                Dim unknown4 = stream.ReadUInt32()
                result.Add(Tuple(b, player, unknown4))
            Loop
            Return result.AsReadableList
        End Function

        Private Shared Sub ReadDataHeader(ByVal stream As IRandomReadableStream)
            Contract.Requires(stream IsNot Nothing)
            Dim unknown1 = stream.ReadByte()
            Dim unknown2 = stream.ReadUInt32()
            Dim host = ReadPlayerRecord(stream)
            Dim gameName = stream.ReadNullTerminatedString(64)
            Dim unknown3 = stream.ReadByte()
            Dim gameStats = stream.ReadPickle(New WC3.GameStatsJar("game stats"))
            Dim numPlayers = stream.ReadUInt32()
            If numPlayers < 1 OrElse numPlayers > 12 Then Throw New IO.InvalidDataException("Invalid number of players.")
            Dim gameType = stream.ReadUInt32()
            Dim lang = stream.ReadUInt32()
            Dim players = ReadHeaderPlayerRecords(stream)
            Dim lobbyState = stream.ReadPickle(WC3.Protocol.Packets.LobbyState)
        End Sub

        Public ReadOnly Property Entries() As IEnumerable(Of ReplayEntry)
            Get
                Contract.Ensures(Contract.Result(Of IEnumerable(Of ReplayEntry))() IsNot Nothing)
                Return New Enumerable(Of ReplayEntry)(Function() EnumerateReplayEntries())
            End Get
        End Property
        Private Function EnumerateReplayEntries() As IEnumerator(Of ReplayEntry)
            Contract.Ensures(Contract.Result(Of IEnumerator(Of ReplayEntry))() IsNot Nothing)
            Dim stream = MakeDataStream()
            Try
                ReadDataHeader(stream)

                Dim blockTypes = New Dictionary(Of ReplayEntryId, IJar(Of Object))()
                blockTypes(ReplayEntryId.PlayerLeft) = Prots.ReplayEntryPlayerLeft.Weaken
                blockTypes(ReplayEntryId.StartBlock1) = Prots.ReplayEntryStartBlock1.Weaken
                blockTypes(ReplayEntryId.StartBlock2) = Prots.ReplayEntryStartBlock2.Weaken
                blockTypes(ReplayEntryId.StartBlock3) = Prots.ReplayEntryStartBlock3.Weaken
                blockTypes(ReplayEntryId.GameStateChecksum) = Prots.ReplayEntryGameStateChecksum.Weaken
                blockTypes(ReplayEntryId.TournamentForcedCountdown) = Prots.ReplayEntryTournamentForcedCountdown.Weaken
                blockTypes(ReplayEntryId.Unknown0x23) = Prots.ReplayEntryUnknown0x23.Weaken
                blockTypes(ReplayEntryId.ChatMessage) = Prots.ReplayEntryChatMessage.Weaken

                'Enumerate Blocks
                Dim time = 0UI
                Return New Enumerator(Of ReplayEntry)(
                    Function(controller)
                        Try
                            If stream.Position = stream.Length Then Return controller.Break
                            Dim blockId = CType(stream.ReadByte(), ReplayEntryId)
                            If blockTypes.ContainsKey(blockId) Then
                                Return New ReplayEntry(blockId, stream.ReadPickle(blockTypes(blockId)))
                            ElseIf blockId = ReplayEntryId.EndOfReplay Then
                                stream.Dispose()
                                Return controller.Break()
                            ElseIf blockId = ReplayEntryId.Tick Then
                                Return controller.Sequence(EnumerateTickActionBlocks(stream, byref_time:=time))
                            Else
                                Throw New IO.InvalidDataException("Unrecognized {0}: {1}".Frmt(GetType(ReplayEntryId), blockId))
                            End If
                        Catch e As Exception
                            stream.Dispose()
                            Throw
                        End Try
                    End Function,
                    disposer:=AddressOf stream.Dispose)
            Catch e As Exception
                stream.Dispose()
                Throw
            End Try
        End Function
        Private Function EnumerateTickActionBlocks(ByVal stream As IRandomReadableStream, ByRef byref_time As UInt32) As IEnumerator(Of ReplayEntry)
            Contract.Requires(stream IsNot Nothing)
            Contract.Ensures(Contract.Result(Of IEnumerator(Of ReplayEntry))() IsNot Nothing)
            Dim blockSizeLeft = stream.ReadUInt16()

            'time
            Dim dt = stream.ReadUInt16()
            Dim t = byref_time
            byref_time += dt
            blockSizeLeft -= 2US

            'actions
            Return New Enumerator(Of ReplayEntry)(
                Function(controller)
                    If blockSizeLeft <= 0 Then Return controller.Break()
                    Dim pid = stream.ReadByte()

                    Dim subActionSize = stream.ReadUInt16()
                    If subActionSize = 0 Then Throw New IO.InvalidDataException("Invalid Action Block Size")
                    blockSizeLeft -= subActionSize + 3US
                    If blockSizeLeft < 0 Then Throw New IO.InvalidDataException("Inconsistent Time Block and Action Block Sizes.")

                    Return New ReplayEntry(ReplayEntryId.Tick, New ReplayGameAction(pid, t, stream.ReadExact(subActionSize)))
                End Function)
        End Function
    End Class
End Namespace
