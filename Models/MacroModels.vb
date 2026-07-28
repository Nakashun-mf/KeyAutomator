Imports System.Text.Json.Serialization

''' <summary>1件のマクロ定義</summary>
Public Class MacroItem
    <JsonPropertyName("id")>
    Public Property Id As Integer

    <JsonPropertyName("name")>
    Public Property Name As String = ""

    <JsonPropertyName("delay_sec")>
    Public Property DelaySec As Double

    <JsonPropertyName("actions")>
    Public Property Actions As List(Of ActionItem) = New List(Of ActionItem)()

    Public Function Clone() As MacroItem
        Dim copy As New MacroItem With {
            .Id = Id,
            .Name = Name,
            .DelaySec = DelaySec,
            .Actions = New List(Of ActionItem)()
        }
        For Each a In Actions
            copy.Actions.Add(a.Clone())
        Next
        Return copy
    End Function

    Public Overrides Function ToString() As String
        Return $"{Id}: {Name} ({DelaySec:0.##}s)"
    End Function
End Class

''' <summary>マクロ内の1アクション</summary>
Public Class ActionItem
    <JsonPropertyName("type")>
    Public Property Type As String = "text"

    <JsonPropertyName("value")>
    Public Property Value As String = ""

    Public Function Clone() As ActionItem
        Return New ActionItem With {
            .Type = Type,
            .Value = Value
        }
    End Function
End Class
