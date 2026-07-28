Imports System.IO
Imports System.Text

''' <summary>エラーログ出力</summary>
Public NotInheritable Class ErrorLogger
    Private Sub New()
    End Sub

    Public Shared Sub Write(ex As Exception, Optional context As String = Nothing)
        Try
            Dim logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "error.log")
            Dim sb As New StringBuilder()
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {If(context, "Error")}")
            sb.AppendLine(ex.ToString())
            sb.AppendLine(New String("-"c, 60))
            File.AppendAllText(logPath, sb.ToString(), Encoding.UTF8)
        Catch
            ' ログ自体の失敗は握りつぶす
        End Try
    End Sub

    Public Shared Sub Write(message As String)
        Try
            Dim logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "error.log")
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}", Encoding.UTF8)
        Catch
        End Try
    End Sub
End Class
