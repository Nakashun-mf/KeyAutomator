Friend Module Program

    <STAThread()>
    Friend Sub Main(args As String())
        If CliRunner.IsCliMode(args) Then
            Dim code = CliRunner.Run(args)
            Environment.ExitCode = code
            Return
        End If

        Application.SetHighDpiMode(HighDpiMode.SystemAware)
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Application.Run(New MainForm())
    End Sub

End Module
