Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Windows.Forms

''' <summary>アクション実行（SendInput）</summary>
Public NotInheritable Class KeySender
    Private Sub New()
    End Sub

    Private Shared ReadOnly KeyMap As New Dictionary(Of String, Keys)(StringComparer.OrdinalIgnoreCase) From {
        {"ENTER", Keys.Enter},
        {"RETURN", Keys.Enter},
        {"TAB", Keys.Tab},
        {"ESC", Keys.Escape},
        {"ESCAPE", Keys.Escape},
        {"BACKSPACE", Keys.Back},
        {"BS", Keys.Back},
        {"DELETE", Keys.Delete},
        {"DEL", Keys.Delete},
        {"INSERT", Keys.Insert},
        {"INS", Keys.Insert},
        {"HOME", Keys.Home},
        {"END", Keys.End},
        {"PAGEUP", Keys.PageUp},
        {"PGUP", Keys.PageUp},
        {"PAGEDOWN", Keys.PageDown},
        {"PGDN", Keys.PageDown},
        {"UP", Keys.Up},
        {"DOWN", Keys.Down},
        {"LEFT", Keys.Left},
        {"RIGHT", Keys.Right},
        {"SPACE", Keys.Space},
        {"F1", Keys.F1}, {"F2", Keys.F2}, {"F3", Keys.F3}, {"F4", Keys.F4},
        {"F5", Keys.F5}, {"F6", Keys.F6}, {"F7", Keys.F7}, {"F8", Keys.F8},
        {"F9", Keys.F9}, {"F10", Keys.F10}, {"F11", Keys.F11}, {"F12", Keys.F12}
    }

    Private Shared ReadOnly ExtendedKeys As New HashSet(Of Keys) From {
        Keys.Up, Keys.Down, Keys.Left, Keys.Right,
        Keys.Home, Keys.End, Keys.PageUp, Keys.PageDown,
        Keys.Insert, Keys.Delete, Keys.RControlKey, Keys.RMenu
    }

    Public Shared Sub ExecuteMacro(macro As MacroItem)
        If macro Is Nothing Then Throw New ArgumentNullException(NameOf(macro))

        Dim delayMs = CInt(Math.Max(0, macro.DelaySec) * 1000)
        If delayMs > 0 Then
            Thread.Sleep(delayMs)
        End If

        For Each action In macro.Actions
            ExecuteAction(action)
        Next
    End Sub

    Public Shared Sub ExecuteAction(action As ActionItem)
        If action Is Nothing Then Return
        Dim t = If(action.Type, "").Trim().ToLowerInvariant()
        Dim v = If(action.Value, "")

        Select Case t
            Case "text"
                SendText(v)
            Case "key"
                SendKey(v)
            Case "hotkey"
                SendHotkey(v)
            Case "wait"
                Dim sec As Double
                If Double.TryParse(v, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, sec) OrElse
                   Double.TryParse(v, sec) Then
                    Thread.Sleep(CInt(Math.Max(0, sec) * 1000))
                End If
            Case Else
                ErrorLogger.Write($"未知のアクション種別: {action.Type}")
        End Select
    End Sub

    Public Shared Sub SendText(text As String)
        If String.IsNullOrEmpty(text) Then Return
        For Each ch In text
            If ch = ChrW(&HA) Then
                ' LF は Enter として扱う（CRLF の CR は別文字）
                SendVirtualKey(Keys.Enter, keyDown:=True)
                SendVirtualKey(Keys.Enter, keyDown:=False)
            ElseIf ch = ChrW(&HD) Then
                ' CR は無視（LF 側で処理）
            Else
                SendUnicodeChar(ch, keyDown:=True)
                SendUnicodeChar(ch, keyDown:=False)
            End If
            Thread.Sleep(5)
        Next
    End Sub

    Public Shared Sub SendKey(keyName As String)
        Dim key = ResolveKey(keyName)
        If key = Keys.None Then
            ErrorLogger.Write($"未対応のキー名: {keyName}")
            Return
        End If
        SendVirtualKey(key, keyDown:=True)
        Thread.Sleep(20)
        SendVirtualKey(key, keyDown:=False)
        Thread.Sleep(10)
    End Sub

    Public Shared Sub SendHotkey(hotkey As String)
        If String.IsNullOrWhiteSpace(hotkey) Then Return

        Dim parts = hotkey.Split({"+"c, " "c}, StringSplitOptions.RemoveEmptyEntries).
            Select(Function(p) p.Trim()).
            Where(Function(p) p.Length > 0).
            ToArray()

        Dim modifiers As New List(Of Keys)()
        Dim mainKey As Keys = Keys.None

        For Each part In parts
            Select Case part.ToUpperInvariant()
                Case "CTRL", "CONTROL", "CTL"
                    modifiers.Add(Keys.ControlKey)
                Case "ALT", "MENU"
                    modifiers.Add(Keys.Menu)
                Case "SHIFT"
                    modifiers.Add(Keys.ShiftKey)
                Case "WIN", "WINDOWS", "LWIN"
                    modifiers.Add(Keys.LWin)
                Case Else
                    mainKey = ResolveKey(part)
            End Select
        Next

        If mainKey = Keys.None Then
            ErrorLogger.Write($"ホットキーのメインキーが不正: {hotkey}")
            Return
        End If

        For Each modKey In modifiers
            SendVirtualKey(modKey, keyDown:=True)
            Thread.Sleep(10)
        Next

        SendVirtualKey(mainKey, keyDown:=True)
        Thread.Sleep(20)
        SendVirtualKey(mainKey, keyDown:=False)
        Thread.Sleep(10)

        For i = modifiers.Count - 1 To 0 Step -1
            SendVirtualKey(modifiers(i), keyDown:=False)
            Thread.Sleep(10)
        Next
    End Sub

    Public Shared Function ResolveKey(name As String) As Keys
        If String.IsNullOrWhiteSpace(name) Then Return Keys.None
        Dim n = name.Trim()

        Dim mapped As Keys
        If KeyMap.TryGetValue(n, mapped) Then Return mapped

        If n.Length = 1 Then
            Dim c = Char.ToUpperInvariant(n(0))
            If c >= "A"c AndAlso c <= "Z"c Then
                Return CType(AscW(c), Keys)
            End If
            If c >= "0"c AndAlso c <= "9"c Then
                Return CType(AscW(c), Keys)
            End If
        End If

        Dim parsed As Keys
        If [Enum].TryParse(n, ignoreCase:=True, result:=parsed) Then
            Return parsed
        End If

        Return Keys.None
    End Function

    Private Shared Sub SendUnicodeChar(ch As Char, keyDown As Boolean)
        Dim input As New NativeMethods.INPUT With {
            .type = NativeMethods.INPUT_KEYBOARD
        }
        input.U.ki.wVk = 0
        input.U.ki.wScan = CUShort(AscW(ch))
        input.U.ki.dwFlags = NativeMethods.KEYEVENTF_UNICODE Or If(keyDown, 0UI, NativeMethods.KEYEVENTF_KEYUP)
        input.U.ki.time = 0
        input.U.ki.dwExtraInfo = IntPtr.Zero
        Send(input)
    End Sub

    Private Shared Sub SendVirtualKey(key As Keys, keyDown As Boolean)
        Dim vk = CUShort(CInt(key) And &HFFFF)
        Dim flags As UInteger = If(keyDown, NativeMethods.KEYEVENTF_KEYDOWN, NativeMethods.KEYEVENTF_KEYUP)
        If ExtendedKeys.Contains(key) Then
            flags = flags Or NativeMethods.KEYEVENTF_EXTENDEDKEY
        End If

        Dim input As New NativeMethods.INPUT With {
            .type = NativeMethods.INPUT_KEYBOARD
        }
        input.U.ki.wVk = vk
        input.U.ki.wScan = 0
        input.U.ki.dwFlags = flags
        input.U.ki.time = 0
        input.U.ki.dwExtraInfo = IntPtr.Zero
        Send(input)
    End Sub

    Private Shared Sub Send(input As NativeMethods.INPUT)
        Dim arr = {input}
        Dim sent = NativeMethods.SendInput(1UI, arr, Marshal.SizeOf(GetType(NativeMethods.INPUT)))
        If sent = 0 Then
            ErrorLogger.Write($"SendInput 失敗 (GetLastError={Runtime.InteropServices.Marshal.GetLastWin32Error()})")
        End If
    End Sub
End Class
