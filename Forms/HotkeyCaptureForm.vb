Imports System.ComponentModel

''' <summary>ショートカットキー記録ダイアログ</summary>
Public Class HotkeyCaptureForm
    Inherits Form

    Private ReadOnly _lblHint As New Label()
    Private ReadOnly _txtResult As New TextBox()
    Private ReadOnly _btnOk As New Button()
    Private ReadOnly _btnCancel As New Button()

    Public Property CapturedHotkey As String = ""

    Public Sub New()
        Text = "ショートカット記録"
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        StartPosition = FormStartPosition.CenterParent
        ClientSize = New Size(420, 160)
        KeyPreview = True

        _lblHint.AutoSize = False
        _lblHint.Location = New Point(16, 16)
        _lblHint.Size = New Size(388, 40)
        _lblHint.Text = "キーを押すと組み合わせが記録されます。" & Environment.NewLine &
                        "例: Ctrl+Shift+S / Alt+Tab"

        _txtResult.Location = New Point(16, 64)
        _txtResult.Size = New Size(388, 27)
        _txtResult.ReadOnly = True
        _txtResult.Font = New Font("Consolas", 11.0F)

        _btnOk.Text = "OK"
        _btnOk.Location = New Point(220, 110)
        _btnOk.Size = New Size(90, 30)
        _btnOk.Enabled = False
        AddHandler _btnOk.Click, Sub()
                                     DialogResult = DialogResult.OK
                                     Close()
                                 End Sub

        _btnCancel.Text = "キャンセル"
        _btnCancel.Location = New Point(314, 110)
        _btnCancel.Size = New Size(90, 30)
        _btnCancel.DialogResult = DialogResult.Cancel

        Controls.AddRange({_lblHint, _txtResult, _btnOk, _btnCancel})
        AcceptButton = _btnOk
        CancelButton = _btnCancel
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        e.SuppressKeyPress = True
        e.Handled = True

        ' 修飾キー単体は無視
        If e.KeyCode = Keys.ControlKey OrElse e.KeyCode = Keys.ShiftKey OrElse
           e.KeyCode = Keys.Menu OrElse e.KeyCode = Keys.LWin OrElse e.KeyCode = Keys.RWin Then
            Return
        End If

        Dim parts As New List(Of String)()
        If e.Control Then parts.Add("CTRL")
        If e.Alt Then parts.Add("ALT")
        If e.Shift Then parts.Add("SHIFT")

        Dim keyName = FormatKey(e.KeyCode)
        If String.IsNullOrEmpty(keyName) Then Return

        parts.Add(keyName)
        CapturedHotkey = String.Join("+", parts)
        _txtResult.Text = CapturedHotkey
        _btnOk.Enabled = True
    End Sub

    Private Shared Function FormatKey(key As Keys) As String
        Select Case key
            Case Keys.Enter : Return "ENTER"
            Case Keys.Tab : Return "TAB"
            Case Keys.Escape : Return "ESC"
            Case Keys.Back : Return "BACKSPACE"
            Case Keys.Delete : Return "DELETE"
            Case Keys.Insert : Return "INSERT"
            Case Keys.Home : Return "HOME"
            Case Keys.End : Return "END"
            Case Keys.PageUp : Return "PAGEUP"
            Case Keys.PageDown : Return "PAGEDOWN"
            Case Keys.Up : Return "UP"
            Case Keys.Down : Return "DOWN"
            Case Keys.Left : Return "LEFT"
            Case Keys.Right : Return "RIGHT"
            Case Keys.Space : Return "SPACE"
            Case Keys.F1 To Keys.F12
                Return key.ToString().ToUpperInvariant()
            Case Else
                Dim code = CInt(key) And &HFFFF
                If code >= AscW("A"c) AndAlso code <= AscW("Z"c) Then
                    Return ChrW(code).ToString()
                End If
                If code >= AscW("0"c) AndAlso code <= AscW("9"c) Then
                    Return ChrW(code).ToString()
                End If
                Return key.ToString().ToUpperInvariant()
        End Select
    End Function
End Class
