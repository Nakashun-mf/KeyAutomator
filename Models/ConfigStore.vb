Imports System.IO
Imports System.Text
Imports System.Text.Json

''' <summary>config.json の読み書き</summary>
Public NotInheritable Class ConfigStore
    Private Sub New()
    End Sub

    Public Shared ReadOnly Property ConfigPath As String
        Get
            Return Path.Combine(AppContext.BaseDirectory, "config.json")
        End Get
    End Property

    Private Shared ReadOnly JsonOptions As New JsonSerializerOptions With {
        .WriteIndented = True,
        .Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    }

    Public Shared Function Load() As List(Of MacroItem)
        Dim path = ConfigPath
        If Not File.Exists(path) Then
            Dim empty As New List(Of MacroItem)()
            Save(empty)
            Return empty
        End If

        Dim json = File.ReadAllText(path, Encoding.UTF8)
        If String.IsNullOrWhiteSpace(json) Then
            Return New List(Of MacroItem)()
        End If

        Dim list = JsonSerializer.Deserialize(Of List(Of MacroItem))(json, JsonOptions)
        If list Is Nothing Then
            Return New List(Of MacroItem)()
        End If
        Return list
    End Function

    Public Shared Sub Save(macros As List(Of MacroItem))
        Dim json = JsonSerializer.Serialize(macros, JsonOptions)
        File.WriteAllText(ConfigPath, json, New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False))
    End Sub

    Public Shared Function FindById(macros As IEnumerable(Of MacroItem), id As Integer) As MacroItem
        Return macros.FirstOrDefault(Function(m) m.Id = id)
    End Function

    Public Shared Function FindByName(macros As IEnumerable(Of MacroItem), name As String) As MacroItem
        Return macros.FirstOrDefault(Function(m) String.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
    End Function

    Public Shared Function NextId(macros As IEnumerable(Of MacroItem)) As Integer
        If Not macros.Any() Then Return 1
        Return macros.Max(Function(m) m.Id) + 1
    End Function
End Class
