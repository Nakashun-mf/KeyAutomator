''' <summary>CLI サイレント実行</summary>
Public NotInheritable Class CliRunner
    Private Sub New()
    End Sub

    ''' <summary>
    ''' 引数を解釈してマクロ実行。成功 0 / 失敗 1。
    ''' </summary>
    Public Shared Function Run(args As String()) As Integer
        Try
            Dim macro = ResolveMacro(args)
            If macro Is Nothing Then
                ErrorLogger.Write($"指定マクロが見つかりません: {String.Join(" ", args)}")
                Return 1
            End If

            KeySender.ExecuteMacro(macro)
            Return 0
        Catch ex As Exception
            ErrorLogger.Write(ex, "CLI実行エラー")
            Return 1
        End Try
    End Function

    ''' <summary>引数ありなら CLI モードと判定</summary>
    Public Shared Function IsCliMode(args As String()) As Boolean
        Return args IsNot Nothing AndAlso args.Length > 0
    End Function

    Public Shared Function ResolveMacro(args As String()) As MacroItem
        Dim macros As List(Of MacroItem)
        Try
            macros = ConfigStore.Load()
        Catch ex As Exception
            ErrorLogger.Write(ex, "config.json 読み込み失敗")
            Return Nothing
        End Try

        If args Is Nothing OrElse args.Length = 0 Then Return Nothing

        ' -id <ID>
        For i = 0 To args.Length - 1
            If String.Equals(args(i), "-id", StringComparison.OrdinalIgnoreCase) AndAlso i + 1 < args.Length Then
                Dim id As Integer
                If Integer.TryParse(args(i + 1), id) Then
                    Return ConfigStore.FindById(macros, id)
                End If
                Return Nothing
            End If
        Next

        ' -name "<登録名>"
        For i = 0 To args.Length - 1
            If String.Equals(args(i), "-name", StringComparison.OrdinalIgnoreCase) AndAlso i + 1 < args.Length Then
                Return ConfigStore.FindByName(macros, args(i + 1))
            End If
        Next

        ' -<ID> （例: -1）
        Dim first = args(0)
        If first.StartsWith("-"c) AndAlso first.Length > 1 Then
            Dim idPart = first.Substring(1)
            Dim id As Integer
            If Integer.TryParse(idPart, id) Then
                Return ConfigStore.FindById(macros, id)
            End If
        End If

        Return Nothing
    End Function
End Class
