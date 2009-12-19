﻿Namespace WC3
    '''<summary>Game actions which can be performed by players.</summary>
    '''<original-source> http://www.wc3c.net/tools/specs/W3GActions.txt </original-source>
    Public Enum W3GameActionId As Byte
        PauseGame = &H1
        ResumeGame = &H2
        SetGameSpeed = &H3
        IncreaseGameSpeed = &H4
        DecreaseGameSpeed = &H5
        SaveGameStarted = &H6
        SaveGameFinished = &H7
        '_Unseen0x08 = &H8
        '_Unseen0x09 = &H9
        '_Unseen0x0A = &HA
        '_Unseen0x0B = &HB
        '_Unseen0x0C = &HC
        '_Unseen0x0D = &HD
        '_Unseen0x0E = &HE
        '_Unseen0x0F = &HF
        SelfOrder = &H10
        PointOrder = &H11
        ObjectOrder = &H12
        DropOrGiveItem = &H13
        FogObjectOrder = &H14
        '_Unseen0x15 = &H15
        ChangeSelection = &H16
        AssignGroupHotkey = &H17
        SelectGroupHotkey = &H18
        SelectSubgroup = &H19
        PreSubGroupSelection = &H1A
        TriggerSelectionEvent = &H1B
        SelectGroundItem = &H1C
        CancelHeroRevive = &H1D
        DequeueBuildingOrder = &H1E
        '_Unseen0x1F = &H1F
        CheatFastCooldown = &H20
        '_Unseen0x21 = &H21
        CheatInstantDefeat = &H22
        CheatSpeedConstruction = &H23
        CheatFastDeathDecay = &H24
        CheatNoFoodLimit = &H25
        CheatGodMode = &H26
        CheatGold = &H27
        CheatLumber = &H28
        CheatUnlimitedMana = &H29
        CheatNoDefeat = &H2A
        CheatDisableVictoryConditions = &H2B
        CheatEnableResearch = &H2C
        CheatGoldAndLumber = &H2D
        CheatSetTimeOfDay = &H2E
        CheatRemoveFogOfWar = &H2F
        CheatDisableTechRequirements = &H30
        CheatResearchUpgrades = &H31
        CheatInstantVictory = &H32
        '_Unseen0x33 = &H33
        '_Unseen0x34 = &H34
        '_Unseen0x35 = &H35
        '_Unseen0x36 = &H36
        '_Unseen0x37 = &H37
        '_Unseen0x38 = &H38
        '_Unseen0x39 = &H39
        '_Unseen0x3A = &H3A
        '_Unseen0x3B = &H3B
        '_Unseen0x3C = &H3C
        '_Unseen0x3D = &H3D
        '_Unseen0x3E = &H3E
        '_Unseen0x3F = &H3F
        '_Unseen0x40 = &H40
        '_Unseen0x41 = &H41
        '_Unseen0x42 = &H42
        '_Unseen0x43 = &H43
        '_Unseen0x44 = &H44
        '_Unseen0x45 = &H45
        '_Unseen0x46 = &H46
        '_Unseen0x47 = &H47
        '_Unseen0x48 = &H48
        '_Unseen0x49 = &H49
        '_Unseen0x4A = &H4A
        '_Unseen0x4B = &H4B
        '_Unseen0x4C = &H4C
        '_Unseen0x4D = &H4D
        '_Unseen0x4E = &H4E
        '_Unseen0x4F = &H4F
        ChangeAllyOptions = &H50
        TransferResources = &H51
        '_Unseen0x52 = &H52
        '_Unseen0x53 = &H53
        '_Unseen0x54 = &H54
        '_Unseen0x55 = &H55
        '_Unseen0x56 = &H56
        '_Unseen0x57 = &H57
        '_Unseen0x58 = &H58
        '_Unseen0x59 = &H59
        '_Unseen0x5A = &H5A
        '_Unseen0x5B = &H5B
        '_Unseen0x5C = &H5C
        '_Unseen0x5D = &H5D
        '_Unseen0x5E = &H5E
        '_Unseen0x5F = &H5F
        TriggerChatEvent = &H60
        PressedEscape = &H61
        TriggerWaitFinished = &H62
        '_Unseen0x63 = &H63
        TriggerMouseClickedTrackable = &H64
        TriggerMouseTouchedTrackable = &H65
        EnterChooseHeroSkillSubmenu = &H66
        EnterChooseBuildingSubmenu = &H67
        MinimapPing = &H68
        TriggerDialogButtonClicked2 = &H69
        TriggerDialogButtonClicked = &H6A
        GameCacheSyncInteger = &H6B
        GameCacheSyncReal = &H6C
        GameCacheSyncBoolean = &H6D
        GameCacheSyncString = &H6E 'this is a guess; the SyncStoredString function does nothing so I've never actually seen the packet
        GameCacheSyncUnit = &H6F
        '_Unseen0x70 = &H70
        '_Unseen0x71 = &H71
        '_Unseen0x72 = &H72
        '_Unseen0x73 = &H73
        '_Unseen0x74 = &H74
        TriggerArrowKeyEvent = &H75
    End Enum

    Public NotInheritable Class GameAction
        Public ReadOnly id As W3GameActionId
        Private ReadOnly _payload As IPickle(Of Object)
        Private Shared ReadOnly packetJar As PrefixSwitchJar(Of W3GameActionId) = MakeJar()

        Private Sub New(ByVal payload As IPickle(Of PrefixPickle(Of W3GameActionId)))
            Contract.Requires(payload IsNot Nothing)
            Me._payload = payload.Value.payload
            Me.id = payload.Value.index
        End Sub

        Public ReadOnly Property Payload As IPickle(Of Object)
            Get
                Contract.Ensures(Contract.Result(Of IPickle(Of Object))() IsNot Nothing)
                Return _payload
            End Get
        End Property

        Public Shared Function FromData(ByVal data As IReadableList(Of Byte)) As GameAction
            Contract.Requires(data IsNot Nothing)
            Contract.Ensures(Contract.Result(Of GameAction)() IsNot Nothing)
            Return New GameAction(packetJar.Parse(data))
        End Function

        Public Overrides Function ToString() As String
            Return "{0} = {1}".Frmt(id, Payload.Description.Value())
        End Function

#Region "Definition"
        Private Shared Sub reg(ByVal jar As PrefixSwitchJar(Of W3GameActionId),
                               ByVal id As W3GameActionId,
                               ByVal ParamArray subJars() As IJar(Of Object))
            Contract.Requires(jar IsNot Nothing)
            Contract.Requires(subJars IsNot Nothing)
            jar.AddPackerParser(id, New TupleJar(id.ToString, subJars).Weaken)
        End Sub
        Private Shared Function MakeJar() As PrefixSwitchJar(Of W3GameActionId)
            Contract.Ensures(Contract.Result(Of PrefixSwitchJar(Of W3GameActionId))() IsNot Nothing)
            Dim jar = New PrefixSwitchJar(Of W3GameActionId)("W3GameAction")

            'Game controls
            reg(jar, W3GameActionId.PauseGame)
            reg(jar, W3GameActionId.ResumeGame)
            reg(jar, W3GameActionId.SetGameSpeed,
                        New EnumByteJar(Of GameSpeedSetting)("speed").Weaken)
            reg(jar, W3GameActionId.IncreaseGameSpeed)
            reg(jar, W3GameActionId.DecreaseGameSpeed)
            reg(jar, W3GameActionId.SaveGameStarted,
                        New StringJar("filename").Weaken)
            reg(jar, W3GameActionId.SaveGameFinished,
                        New UInt32Jar("unknown").Weaken)

            'Orders
            reg(jar, W3GameActionId.SelfOrder,
                        New EnumUInt16Jar(Of OrderTypes)("flags").Weaken,
                        New OrderTypeJar("order").Weaken,
                        New ObjectIdJar("unknown").Weaken)
            reg(jar, W3GameActionId.PointOrder,
                        New EnumUInt16Jar(Of OrderTypes)("flags").Weaken,
                        New OrderTypeJar("order").Weaken,
                        New ObjectIdJar("unknown").Weaken,
                        New FloatSingleJar("target x").Weaken,
                        New FloatSingleJar("target y").Weaken)
            reg(jar, W3GameActionId.ObjectOrder,
                        New EnumUInt16Jar(Of OrderTypes)("flags").Weaken,
                        New OrderTypeJar("order").Weaken,
                        New ObjectIdJar("unknown").Weaken,
                        New FloatSingleJar("x").Weaken,
                        New FloatSingleJar("y").Weaken,
                        New ObjectIdJar("target").Weaken)
            reg(jar, W3GameActionId.DropOrGiveItem,
                        New EnumUInt16Jar(Of OrderTypes)("flags").Weaken,
                        New OrderTypeJar("order").Weaken,
                        New ObjectIdJar("unknown").Weaken,
                        New FloatSingleJar("x").Weaken,
                        New FloatSingleJar("y").Weaken,
                        New ObjectIdJar("receiver").Weaken,
                        New ObjectIdJar("item").Weaken)
            reg(jar, W3GameActionId.FogObjectOrder,
                        New EnumUInt16Jar(Of OrderTypes)("flags").Weaken,
                        New OrderTypeJar("order").Weaken,
                        New ObjectIdJar("unknown").Weaken,
                        New FloatSingleJar("fog target x").Weaken,
                        New FloatSingleJar("fog target y").Weaken,
                        New ObjectTypeJar("fog target type").Weaken,
                        New ArrayJar("unknown2", expectedSize:=9).Weaken,
                        New FloatSingleJar("actual target x").Weaken,
                        New FloatSingleJar("actual target y").Weaken)

            'Other orders
            reg(jar, W3GameActionId.EnterChooseHeroSkillSubmenu)
            reg(jar, W3GameActionId.EnterChooseBuildingSubmenu)
            reg(jar, W3GameActionId.PressedEscape)
            reg(jar, W3GameActionId.CancelHeroRevive,
                        New ObjectIdJar("target").Weaken)
            reg(jar, W3GameActionId.DequeueBuildingOrder,
                        New ByteJar("slot number").Weaken,
                        New ObjectTypeJar("type").Weaken)
            reg(jar, W3GameActionId.MinimapPing,
                        New FloatSingleJar("x").Weaken,
                        New FloatSingleJar("y").Weaken,
                        New ArrayJar("unknown", expectedSize:=4).Weaken)

            'Alliance
            reg(jar, W3GameActionId.ChangeAllyOptions,
                        New ByteJar("player slot id").Weaken,
                        New EnumUInt32Jar(Of AllianceTypes)("flags").Weaken)
            reg(jar, W3GameActionId.TransferResources,
                        New ByteJar("player slot id").Weaken,
                        New UInt32Jar("gold").Weaken,
                        New UInt32Jar("lumber").Weaken)

            'Selection
            reg(jar, W3GameActionId.ChangeSelection,
                        New EnumByteJar(Of SelectionOperation)("operation").Weaken,
                        New ListJar(Of Object)("targets",
                            New ObjectIdJar("target").Weaken, prefixsize:=2).Weaken)
            reg(jar, W3GameActionId.AssignGroupHotkey,
                        New ByteJar("group index").Weaken,
                        New ListJar(Of Object)("targets",
                            New ObjectIdJar("target").Weaken, prefixsize:=2).Weaken)
            reg(jar, W3GameActionId.SelectGroupHotkey,
                        New ByteJar("group index").Weaken,
                        New ByteJar("unknown").Weaken)
            reg(jar, W3GameActionId.SelectSubgroup,
                        New ObjectTypeJar("unit type").Weaken,
                        New ObjectIdJar("target").Weaken)
            reg(jar, W3GameActionId.PreSubGroupSelection)
            reg(jar, W3GameActionId.SelectGroundItem,
                        New ByteJar("unknown").Weaken,
                        New ObjectIdJar("target").Weaken)

            'Cheats
            reg(jar, W3GameActionId.CheatDisableTechRequirements)
            reg(jar, W3GameActionId.CheatDisableVictoryConditions)
            reg(jar, W3GameActionId.CheatEnableResearch)
            reg(jar, W3GameActionId.CheatFastCooldown)
            reg(jar, W3GameActionId.CheatFastDeathDecay)
            reg(jar, W3GameActionId.CheatGodMode)
            reg(jar, W3GameActionId.CheatInstantDefeat)
            reg(jar, W3GameActionId.CheatInstantVictory)
            reg(jar, W3GameActionId.CheatNoDefeat)
            reg(jar, W3GameActionId.CheatNoFoodLimit)
            reg(jar, W3GameActionId.CheatRemoveFogOfWar)
            reg(jar, W3GameActionId.CheatResearchUpgrades)
            reg(jar, W3GameActionId.CheatSpeedConstruction)
            reg(jar, W3GameActionId.CheatUnlimitedMana)
            reg(jar, W3GameActionId.CheatSetTimeOfDay,
                        New FloatSingleJar("time").Weaken)
            reg(jar, W3GameActionId.CheatGold,
                        New ByteJar("unknown").Weaken,
                        New UInt32Jar("amount").Weaken)
            reg(jar, W3GameActionId.CheatGoldAndLumber,
                        New ByteJar("unknown").Weaken,
                        New UInt32Jar("amount").Weaken)
            reg(jar, W3GameActionId.CheatLumber,
                        New ByteJar("unknown").Weaken,
                        New UInt32Jar("amount").Weaken)

            'Triggers
            reg(jar, W3GameActionId.TriggerChatEvent,
                        New ObjectIdJar("trigger event").Weaken,
                        New StringJar("text").Weaken)
            reg(jar, W3GameActionId.TriggerWaitFinished,
                        New ObjectIdJar("trigger thread").Weaken,
                        New UInt32Jar("thread wait count").Weaken)
            reg(jar, W3GameActionId.TriggerMouseTouchedTrackable,
                        New ObjectIdJar("trackable").Weaken)
            reg(jar, W3GameActionId.TriggerMouseClickedTrackable,
                        New ObjectIdJar("trackable").Weaken)
            reg(jar, W3GameActionId.TriggerDialogButtonClicked,
                        New ObjectIdJar("dialog").Weaken,
                        New ObjectIdJar("button").Weaken)
            reg(jar, W3GameActionId.TriggerDialogButtonClicked2,
                        New ObjectIdJar("button").Weaken,
                        New ObjectIdJar("dialog").Weaken)
            reg(jar, W3GameActionId.TriggerArrowKeyEvent,
                        New EnumByteJar(Of ArrowKeyEvent)("event type").Weaken)
            reg(jar, W3GameActionId.TriggerSelectionEvent,
                        New EnumByteJar(Of SelectionOperation)("operation").Weaken,
                        New ObjectIdJar("target").Weaken)

            'Game Cache
            reg(jar, W3GameActionId.GameCacheSyncInteger,
                        New StringJar("filename").Weaken,
                        New StringJar("mission key").Weaken,
                        New StringJar("key").Weaken,
                        New UInt32Jar("value").Weaken)
            reg(jar, W3GameActionId.GameCacheSyncBoolean,
                        New StringJar("filename").Weaken,
                        New StringJar("mission key").Weaken,
                        New StringJar("key").Weaken,
                        New UInt32Jar("value").Weaken)
            reg(jar, W3GameActionId.GameCacheSyncReal,
                        New StringJar("filename").Weaken,
                        New StringJar("mission key").Weaken,
                        New StringJar("key").Weaken,
                        New FloatSingleJar("value").Weaken)
            reg(jar, W3GameActionId.GameCacheSyncUnit,
                        New StringJar("filename").Weaken,
                        New StringJar("mission key").Weaken,
                        New StringJar("key").Weaken,
                        New ObjectTypeJar("unit type").Weaken,
                        New ArrayJar("unknown data", expectedSize:=86).Weaken)
            reg(jar, W3GameActionId.GameCacheSyncString,
                        New StringJar("filename").Weaken,
                        New StringJar("mission key").Weaken,
                        New StringJar("key").Weaken,
                        New StringJar("value").Weaken) 'this is a guess based on the other syncs; I've never actually seen this packet.

            Return jar
        End Function
#End Region

#Region "Enums"
        Public Enum GameSpeedSetting As Byte
            Slow = 0
            Normal = 1
            Fast = 2
        End Enum

        <Flags()>
        Public Enum OrderTypes As UShort
            Queue = 1 << 0
            Train = 1 << 1
            Construct = 1 << 2
            Group = 1 << 3
            NoFormation = 1 << 4
            SubGroup = 1 << 6
            AutoCastOn = 1 << 8
        End Enum

        Public Enum SelectionOperation As Byte
            Add = 1
            Remove = 2
        End Enum

        <Flags()>
        Public Enum AllianceTypes As UInteger
            Passive = 1 << 0
            HelpRequest = 1 << 1
            HelpResponse = 1 << 2
            SharedXP = 1 << 3
            SharedSpells = 1 << 4
            SharedVision = 1 << 5
            SharedControl = 1 << 6
            FullSharedControl = 1 << 7
            Rescuable = 1 << 8
            SharedVisionForced = 1 << 9
            AlliedVictory = 1 << 10
        End Enum

        Public Enum ArrowKeyEvent As Byte
            PressedLeftArrow = 0
            ReleasedLeftArrow = 1
            PressedRightArrow = 2
            ReleasedRightArrow = 3
            PressedDownArrow = 4
            ReleasedDownArrow = 5
            PressedUpArrow = 6
            ReleasedUpArrow = 7
        End Enum

        Public Enum OrderId As UInteger
            Smart = &HD0003 'right-click
            [Stop] = &HD0004
            SetRallyPoint = &HD000C
            GetItem = &HD000D
            Attack = &HD000F
            AttackGround = &HD0010
            AttackOnce = &HD0011
            Move = &HD0012
            AIMove = &HD0014
            Patrol = &HD0016
            HoldPosition = &HD0019
            Build = &HD001A
            HumanBuild = &HD001B
            OrcBuild = &HD001C
            NightElfBuild = &HD001D
            UndeadBuild = &HD001E
            ResumeBuild = &HD001F
            GiveOrDropItem = &HD0021
            SwapItemWithItemInSlot1 = &HD0022
            SwapItemWithItemInSlot2 = &HD0023
            SwapItemWithItemInSlot3 = &HD0024
            SwapItemWithItemInSlot4 = &HD0025
            SwapItemWithItemInSlot5 = &HD0026
            SwapItemWithItemInSlot6 = &HD0027
            UseItemInSlot1 = &HD0028
            UseItemInSlot2 = &HD0029
            UseItemInSlot3 = &HD002A
            UseItemInSlot4 = &HD002B
            UseItemInSlot5 = &HD002C
            UseItemInSlot6 = &HD002D
            ResumeHarvest = &HD0031
            Harvest = &HD0032
            ReturnResources = &HD0034
            AutoHarvestGold = &HD0035
            AutoHarvestLumber = &HD0036
            NeutralDetectAOE = &HD0037
            Repair = &HD0038
            RepairOn = &HD0039
            RepairOff = &HD003A
            '... many many more ...
        End Enum
#End Region

        Public Structure ObjectId
            Public ReadOnly AllocatedId As UInteger
            Public ReadOnly CounterId As UInteger
            Public Sub New(ByVal allocatedId As UInteger, ByVal counterId As UInteger)
                Me.AllocatedId = allocatedId
                Me.CounterId = counterId
            End Sub
        End Structure

        <Pure()>
        Public Shared Function TypeIdString(ByVal value As UInt32) As String
            Dim bytes = value.Bytes()
            If (From b In bytes Where b < 32 Or b >= 128).None Then
                'Ascii identifier (eg. 'hfoo' for human footman)
                Return bytes.Reverse.ParseChrString(nullTerminated:=False)
            Else
                'Not ascii values, better just output hex
                Return bytes.ToHexString
            End If
        End Function

#Region "Jars"
        Private NotInheritable Class ObjectIdJar
            Inherits Jar(Of ObjectId)

            Public Sub New(ByVal name As InvariantString)
                MyBase.New(name)
            End Sub

            Public Overrides Function Pack(Of R As ObjectId)(ByVal value As R) As IPickle(Of R)
                Dim valued As ObjectId = value
                Dim data = Concat(valued.AllocatedId.Bytes, valued.CounterId.Bytes).AsReadableList
                Return New Pickle(Of R)(Me.Name, value, data, Function() ValueToString(valued))
            End Function

            Public Overrides Function Parse(ByVal data As IReadableList(Of Byte)) As IPickle(Of ObjectId)
                If data.Count < 8 Then Throw New PicklingException("Not enough data.")
                Dim datum = data.SubView(0, 8)
                Dim value = New ObjectId(datum.SubView(0, 4).ToUInt32,
                                         datum.SubView(4, 4).ToUInt32)
                Return New Pickle(Of ObjectId)(Me.Name, value, datum, Function() ValueToString(value))
            End Function

            Private Function ValueToString(ByVal value As ObjectId) As String
                If value.AllocatedId = UInt32.MaxValue AndAlso value.CounterId = UInt32.MaxValue Then Return "[none]"
                If value.AllocatedId = value.CounterId Then Return "preplaced id = {0}".Frmt(value.AllocatedId)
                Return "allocated id = {0}, counter id = {1}".Frmt(value.AllocatedId, value.CounterId)
            End Function
        End Class

        Private NotInheritable Class OrderTypeJar
            Inherits EnumUInt32Jar(Of OrderId)

            Public Sub New(ByVal name As InvariantString)
                MyBase.New(name)
            End Sub

            Protected Overrides Function ValueToString(ByVal value As OrderId) As String
                If value >= &HD0000 AndAlso value < &HE0000 Then
                    Return MyBase.ValueToString(value)
                Else
                    Return TypeIdString(value)
                End If
            End Function
        End Class

        Private NotInheritable Class ObjectTypeJar
            Inherits UInt32Jar

            Public Sub New(ByVal name As InvariantString)
                MyBase.New(name)
            End Sub

            Protected Overrides Function ValueToString(ByVal value As UInteger) As String
                Return TypeIdString(value)
            End Function
        End Class
#End Region
    End Class

    Public NotInheritable Class W3GameActionJar
        Inherits Jar(Of GameAction)
        Public Sub New(ByVal name As InvariantString)
            MyBase.New(name)
        End Sub

        Public Overrides Function Pack(Of TValue As GameAction)(ByVal value As TValue) As Pickling.IPickle(Of TValue)
            Return New Pickle(Of TValue)(Name, value, Concat({value.id}, value.Payload.Data.ToArray).AsReadableList)
        End Function

        Public Overrides Function Parse(ByVal data As IReadableList(Of Byte)) As Pickling.IPickle(Of GameAction)
            Dim val = GameAction.FromData(data)
            Dim n = val.Payload.Data.Count
            Return New Pickle(Of GameAction)(Name, val, data.SubView(0, n + 1))
        End Function
    End Class
End Namespace