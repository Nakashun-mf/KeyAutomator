Imports System.Runtime.InteropServices

''' <summary>Win32 SendInput 関連定義</summary>
Public NotInheritable Class NativeMethods
    Private Sub New()
    End Sub

    Public Const INPUT_KEYBOARD As UInteger = 1
    Public Const KEYEVENTF_KEYDOWN As UInteger = &H0UI
    Public Const KEYEVENTF_EXTENDEDKEY As UInteger = &H1UI
    Public Const KEYEVENTF_KEYUP As UInteger = &H2UI
    Public Const KEYEVENTF_UNICODE As UInteger = &H4UI
    Public Const KEYEVENTF_SCANCODE As UInteger = &H8UI

    <StructLayout(LayoutKind.Sequential)>
    Public Structure INPUT
        Public type As UInteger
        Public U As InputUnion
    End Structure

    <StructLayout(LayoutKind.Explicit)>
    Public Structure InputUnion
        <FieldOffset(0)> Public mi As MOUSEINPUT
        <FieldOffset(0)> Public ki As KEYBDINPUT
        <FieldOffset(0)> Public hi As HARDWAREINPUT
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Public Structure MOUSEINPUT
        Public dx As Integer
        Public dy As Integer
        Public mouseData As UInteger
        Public dwFlags As UInteger
        Public time As UInteger
        Public dwExtraInfo As IntPtr
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Public Structure KEYBDINPUT
        Public wVk As UShort
        Public wScan As UShort
        Public dwFlags As UInteger
        Public time As UInteger
        Public dwExtraInfo As IntPtr
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Public Structure HARDWAREINPUT
        Public uMsg As UInteger
        Public wParamL As UShort
        Public wParamH As UShort
    End Structure

    <DllImport("user32.dll", SetLastError:=True)>
    Public Shared Function SendInput(nInputs As UInteger, pInputs() As INPUT, cbSize As Integer) As UInteger
    End Function

    <DllImport("user32.dll")>
    Public Shared Function MapVirtualKey(uCode As UInteger, uMapType As UInteger) As UInteger
    End Function
End Class
