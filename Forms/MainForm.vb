Imports System.ComponentModel

''' <summary>マクロ管理メイン画面</summary>
Public Class MainForm
    Inherits Form

    Private _macros As List(Of MacroItem) = New List(Of MacroItem)()
    Private _current As MacroItem = Nothing
    Private _loading As Boolean = False

    Private ReadOnly _lstMacros As New ListBox()
    Private ReadOnly _btnNew As New Button()
    Private ReadOnly _btnClone As New Button()
    Private ReadOnly _btnDelete As New Button()

    Private ReadOnly _numId As New NumericUpDown()
    Private ReadOnly _txtName As New TextBox()
    Private ReadOnly _numDelay As New NumericUpDown()
    Private ReadOnly _grid As New DataGridView()

    Private ReadOnly _btnAddText As New Button()
    Private ReadOnly _btnAddKey As New Button()
    Private ReadOnly _btnAddHotkey As New Button()
    Private ReadOnly _btnAddWait As New Button()
    Private ReadOnly _btnMoveUp As New Button()
    Private ReadOnly _btnMoveDown As New Button()
    Private ReadOnly _btnRemoveAction As New Button()

    Private ReadOnly _btnSave As New Button()
    Private ReadOnly _btnCancel As New Button()
    Private ReadOnly _btnTest As New Button()
    Private ReadOnly _lblStatus As New Label()

    Public Sub New()
        Text = $"KeyAutomator v{GetType(MainForm).Assembly.GetName().Version}"
        StartPosition = FormStartPosition.CenterScreen
        MinimumSize = New Size(900, 560)
        ClientSize = New Size(1000, 620)

        BuildUi()
        LoadMacros()
    End Sub

    Private Sub BuildUi()
        Dim split As New SplitContainer() With {
            .Dock = DockStyle.Fill,
            .SplitterDistance = 280,
            .FixedPanel = FixedPanel.Panel1
        }

        ' ===== 左パネル =====
        Dim leftPanel As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(8)}
        Dim leftButtons As New FlowLayoutPanel() With {
            .Dock = DockStyle.Bottom,
            .Height = 40,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False
        }

        _lstMacros.Dock = DockStyle.Fill
        _lstMacros.DisplayMember = "ToString"
        AddHandler _lstMacros.SelectedIndexChanged, AddressOf OnMacroSelected

        StyleButton(_btnNew, "新規作成")
        StyleButton(_btnClone, "複製")
        StyleButton(_btnDelete, "削除")
        AddHandler _btnNew.Click, AddressOf OnNew
        AddHandler _btnClone.Click, AddressOf OnClone
        AddHandler _btnDelete.Click, AddressOf OnDelete
        leftButtons.Controls.AddRange({_btnNew, _btnClone, _btnDelete})

        leftPanel.Controls.Add(leftButtons)
        leftPanel.Controls.Add(_lstMacros)
        split.Panel1.Controls.Add(leftPanel)

        ' ===== 右パネル =====
        Dim right As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(12)}

        Dim info As New TableLayoutPanel() With {
            .Dock = DockStyle.Top,
            .Height = 100,
            .ColumnCount = 2,
            .RowCount = 3
        }
        info.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 140))
        info.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        For i = 0 To 2
            info.RowStyles.Add(New RowStyle(SizeType.Absolute, 32))
        Next

        _numId.Minimum = 1
        _numId.Maximum = 999999
        _numId.Dock = DockStyle.Fill
        _txtName.Dock = DockStyle.Fill
        _numDelay.DecimalPlaces = 1
        _numDelay.Minimum = 0
        _numDelay.Maximum = 3600
        _numDelay.Increment = 0.5D
        _numDelay.Dock = DockStyle.Left
        _numDelay.Width = 120

        info.Controls.Add(New Label() With {.Text = "ID", .TextAlign = ContentAlignment.MiddleLeft, .Dock = DockStyle.Fill}, 0, 0)
        info.Controls.Add(_numId, 1, 0)
        info.Controls.Add(New Label() With {.Text = "マクロ名", .TextAlign = ContentAlignment.MiddleLeft, .Dock = DockStyle.Fill}, 0, 1)
        info.Controls.Add(_txtName, 1, 1)
        info.Controls.Add(New Label() With {.Text = "起動前ウェイト(秒)", .TextAlign = ContentAlignment.MiddleLeft, .Dock = DockStyle.Fill}, 0, 2)
        info.Controls.Add(_numDelay, 1, 2)

        Dim actionButtons As New FlowLayoutPanel() With {
            .Dock = DockStyle.Top,
            .Height = 40,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .Padding = New Padding(0, 4, 0, 4)
        }
        StyleButton(_btnAddText, "＋テキスト")
        StyleButton(_btnAddKey, "＋特殊キー")
        StyleButton(_btnAddHotkey, "＋ショートカット")
        StyleButton(_btnAddWait, "＋ウェイト")
        StyleButton(_btnMoveUp, "▲ 上へ")
        StyleButton(_btnMoveDown, "▼ 下へ")
        StyleButton(_btnRemoveAction, "削除")
        AddHandler _btnAddText.Click, Sub() AddAction("text", "")
        AddHandler _btnAddKey.Click, Sub() AddAction("key", "ENTER")
        AddHandler _btnAddHotkey.Click, AddressOf OnAddHotkey
        AddHandler _btnAddWait.Click, Sub() AddAction("wait", "0.5")
        AddHandler _btnMoveUp.Click, Sub() MoveAction(-1)
        AddHandler _btnMoveDown.Click, Sub() MoveAction(1)
        AddHandler _btnRemoveAction.Click, AddressOf OnRemoveAction
        actionButtons.Controls.AddRange({
            _btnAddText, _btnAddKey, _btnAddHotkey, _btnAddWait,
            _btnMoveUp, _btnMoveDown, _btnRemoveAction
        })

        ConfigureGrid()
        _grid.Dock = DockStyle.Fill

        Dim bottom As New Panel() With {.Dock = DockStyle.Bottom, .Height = 48}
        StyleButton(_btnSave, "保存")
        StyleButton(_btnCancel, "キャンセル")
        StyleButton(_btnTest, "テスト実行")
        _btnSave.Location = New Point(0, 8)
        _btnCancel.Location = New Point(100, 8)
        _btnTest.Location = New Point(200, 8)
        AddHandler _btnSave.Click, AddressOf OnSave
        AddHandler _btnCancel.Click, AddressOf OnCancelEdit
        AddHandler _btnTest.Click, AddressOf OnTest

        _lblStatus.AutoSize = False
        _lblStatus.Location = New Point(320, 12)
        _lblStatus.Size = New Size(360, 24)
        _lblStatus.ForeColor = Color.DimGray

        bottom.Controls.AddRange({_btnSave, _btnCancel, _btnTest, _lblStatus})

        ' Dock は Bottom → Top → Fill の順で追加
        right.Controls.Add(bottom)
        right.Controls.Add(info)
        right.Controls.Add(actionButtons)
        right.Controls.Add(_grid)

        split.Panel2.Controls.Add(right)
        Controls.Add(split)

        SetEditorEnabled(False)
    End Sub

    Private Shared Sub StyleButton(btn As Button, caption As String)
        btn.Text = caption
        btn.AutoSize = True
        btn.Padding = New Padding(8, 4, 8, 4)
        btn.Margin = New Padding(2)
    End Sub

    Private Sub ConfigureGrid()
        _grid.AllowUserToAddRows = False
        _grid.AllowUserToDeleteRows = False
        _grid.AllowUserToResizeRows = False
        _grid.MultiSelect = False
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        _grid.RowHeadersVisible = False
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        _grid.Columns.Clear()

        Dim colOrder As New DataGridViewTextBoxColumn() With {
            .Name = "colOrder",
            .HeaderText = "#",
            .FillWeight = 15,
            .ReadOnly = True
        }
        Dim colType As New DataGridViewComboBoxColumn() With {
            .Name = "colType",
            .HeaderText = "種別",
            .FillWeight = 30
        }
        colType.Items.AddRange({"text", "key", "hotkey", "wait"})

        Dim colValue As New DataGridViewTextBoxColumn() With {
            .Name = "colValue",
            .HeaderText = "設定値",
            .FillWeight = 55
        }

        _grid.Columns.AddRange({colOrder, colType, colValue})
        AddHandler _grid.CellValueChanged, AddressOf OnGridChanged
        AddHandler _grid.CurrentCellDirtyStateChanged,
            Sub()
                If _grid.IsCurrentCellDirty Then _grid.CommitEdit(DataGridViewDataErrorContexts.Commit)
            End Sub
        AddHandler _grid.DataError, Sub(s, e) e.ThrowException = False
    End Sub

    Private Sub LoadMacros()
        Try
            _macros = ConfigStore.Load()
        Catch ex As Exception
            MessageBox.Show(Me, $"config.json の読み込みに失敗しました。{Environment.NewLine}{ex.Message}",
                            "読み込みエラー", MessageBoxButtons.OK, MessageBoxIcon.Error)
            ErrorLogger.Write(ex, "GUI Load")
            _macros = New List(Of MacroItem)()
            Try
                ConfigStore.Save(_macros)
            Catch
            End Try
        End Try
        RefreshList()
        SetStatus($"読込完了: {_macros.Count} 件")
    End Sub

    Private Sub RefreshList(Optional selectId As Integer? = Nothing)
        _loading = True
        _lstMacros.BeginUpdate()
        Try
            _lstMacros.Items.Clear()
            For Each m In _macros.OrderBy(Function(x) x.Id)
                _lstMacros.Items.Add(m)
            Next
        Finally
            _lstMacros.EndUpdate()
            _loading = False
        End Try

        If selectId.HasValue Then
            For i = 0 To _lstMacros.Items.Count - 1
                Dim m = DirectCast(_lstMacros.Items(i), MacroItem)
                If m.Id = selectId.Value Then
                    _lstMacros.SelectedIndex = i
                    Return
                End If
            Next
        End If

        If _lstMacros.Items.Count > 0 AndAlso _lstMacros.SelectedIndex < 0 Then
            _lstMacros.SelectedIndex = 0
        ElseIf _lstMacros.Items.Count = 0 Then
            _current = Nothing
            ClearEditor()
            SetEditorEnabled(False)
        End If
    End Sub

    Private Sub OnMacroSelected(sender As Object, e As EventArgs)
        If _loading Then Return
        If _lstMacros.SelectedItem Is Nothing Then
            _current = Nothing
            ClearEditor()
            SetEditorEnabled(False)
            Return
        End If
        Dim selected = DirectCast(_lstMacros.SelectedItem, MacroItem)
        _current = selected.Clone()
        BindEditor(_current)
        SetEditorEnabled(True)
    End Sub

    Private Sub BindEditor(m As MacroItem)
        _loading = True
        Try
            _numId.Value = m.Id
            _txtName.Text = m.Name
            _numDelay.Value = CDec(Math.Min(CDbl(_numDelay.Maximum), Math.Max(0, m.DelaySec)))
            _grid.Rows.Clear()
            Dim i = 1
            For Each a In m.Actions
                Dim idx = _grid.Rows.Add(i.ToString(), a.Type, a.Value)
                i += 1
            Next
        Finally
            _loading = False
        End Try
    End Sub

    Private Sub ClearEditor()
        _loading = True
        Try
            _numId.Value = 1
            _txtName.Text = ""
            _numDelay.Value = 0
            _grid.Rows.Clear()
        Finally
            _loading = False
        End Try
    End Sub

    Private Sub SetEditorEnabled(enabled As Boolean)
        _numId.Enabled = enabled
        _txtName.Enabled = enabled
        _numDelay.Enabled = enabled
        _grid.Enabled = enabled
        _btnAddText.Enabled = enabled
        _btnAddKey.Enabled = enabled
        _btnAddHotkey.Enabled = enabled
        _btnAddWait.Enabled = enabled
        _btnMoveUp.Enabled = enabled
        _btnMoveDown.Enabled = enabled
        _btnRemoveAction.Enabled = enabled
        _btnSave.Enabled = enabled
        _btnCancel.Enabled = enabled
        _btnTest.Enabled = enabled
        _btnClone.Enabled = enabled AndAlso _lstMacros.SelectedItem IsNot Nothing
        _btnDelete.Enabled = enabled AndAlso _lstMacros.SelectedItem IsNot Nothing
    End Sub

    Private Sub SyncCurrentFromEditor()
        If _current Is Nothing Then Return
        _current.Id = CInt(_numId.Value)
        _current.Name = _txtName.Text.Trim()
        _current.DelaySec = CDbl(_numDelay.Value)
        _current.Actions.Clear()
        For Each row As DataGridViewRow In _grid.Rows
            If row.IsNewRow Then Continue For
            Dim t = Convert.ToString(row.Cells("colType").Value)
            Dim v = Convert.ToString(row.Cells("colValue").Value)
            If String.IsNullOrWhiteSpace(t) Then Continue For
            _current.Actions.Add(New ActionItem With {.Type = t, .Value = If(v, "")})
        Next
    End Sub

    Private Sub RenumberGrid()
        For i = 0 To _grid.Rows.Count - 1
            If _grid.Rows(i).IsNewRow Then Continue For
            _grid.Rows(i).Cells("colOrder").Value = (i + 1).ToString()
        Next
    End Sub

    Private Sub AddAction(typeName As String, value As String)
        If _current Is Nothing Then Return
        Dim idx = _grid.Rows.Add((_grid.Rows.Count + 1).ToString(), typeName, value)
        _grid.ClearSelection()
        _grid.Rows(idx).Selected = True
        RenumberGrid()
    End Sub

    Private Sub OnAddHotkey(sender As Object, e As EventArgs)
        Using dlg As New HotkeyCaptureForm()
            If dlg.ShowDialog(Me) = DialogResult.OK AndAlso Not String.IsNullOrWhiteSpace(dlg.CapturedHotkey) Then
                AddAction("hotkey", dlg.CapturedHotkey)
            End If
        End Using
    End Sub

    Private Sub MoveAction(delta As Integer)
        If _grid.SelectedRows.Count = 0 Then Return
        Dim idx = _grid.SelectedRows(0).Index
        Dim newIdx = idx + delta
        If newIdx < 0 OrElse newIdx >= _grid.Rows.Count Then Return

        Dim t = Convert.ToString(_grid.Rows(idx).Cells("colType").Value)
        Dim v = Convert.ToString(_grid.Rows(idx).Cells("colValue").Value)
        _grid.Rows.RemoveAt(idx)
        _grid.Rows.Insert(newIdx, (newIdx + 1).ToString(), t, v)
        _grid.ClearSelection()
        _grid.Rows(newIdx).Selected = True
        RenumberGrid()
    End Sub

    Private Sub OnRemoveAction(sender As Object, e As EventArgs)
        If _grid.SelectedRows.Count = 0 Then Return
        _grid.Rows.RemoveAt(_grid.SelectedRows(0).Index)
        RenumberGrid()
    End Sub

    Private Sub OnGridChanged(sender As Object, e As DataGridViewCellEventArgs)
        If _loading OrElse e.RowIndex < 0 Then Return
        RenumberGrid()
    End Sub

    Private Sub OnNew(sender As Object, e As EventArgs)
        Dim item As New MacroItem With {
            .Id = ConfigStore.NextId(_macros),
            .Name = "新しいマクロ",
            .DelaySec = 3.0,
            .Actions = New List(Of ActionItem)()
        }
        _macros.Add(item)
        PersistAll()
        RefreshList(item.Id)
        SetStatus($"新規作成: ID {item.Id}")
    End Sub

    Private Sub OnClone(sender As Object, e As EventArgs)
        If _lstMacros.SelectedItem Is Nothing Then Return
        Dim src = DirectCast(_lstMacros.SelectedItem, MacroItem)
        Dim copy = src.Clone()
        copy.Id = ConfigStore.NextId(_macros)
        copy.Name = src.Name & " (コピー)"
        _macros.Add(copy)
        PersistAll()
        RefreshList(copy.Id)
        SetStatus($"複製: ID {copy.Id}")
    End Sub

    Private Sub OnDelete(sender As Object, e As EventArgs)
        If _lstMacros.SelectedItem Is Nothing Then Return
        Dim src = DirectCast(_lstMacros.SelectedItem, MacroItem)
        If MessageBox.Show(Me, $"ID {src.Id}「{src.Name}」を削除しますか？", "確認",
                           MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then
            Return
        End If
        _macros.RemoveAll(Function(m) m.Id = src.Id)
        PersistAll()
        _current = Nothing
        RefreshList()
        SetStatus("削除しました")
    End Sub

    Private Sub OnSave(sender As Object, e As EventArgs)
        If _current Is Nothing Then Return
        SyncCurrentFromEditor()

        If String.IsNullOrWhiteSpace(_current.Name) Then
            MessageBox.Show(Me, "マクロ名を入力してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim original = TryCast(_lstMacros.SelectedItem, MacroItem)
        Dim originalId = If(original IsNot Nothing, original.Id, _current.Id)

        ' ID 重複チェック（自分以外）
        If _macros.Any(Function(m) m.Id = _current.Id AndAlso m.Id <> originalId) Then
            MessageBox.Show(Me, $"ID {_current.Id} は既に使用されています。", "入力エラー",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim idx = _macros.FindIndex(Function(m) m.Id = originalId)
        If idx < 0 Then
            _macros.Add(_current.Clone())
        Else
            _macros(idx) = _current.Clone()
        End If

        PersistAll()
        RefreshList(_current.Id)
        SetStatus("保存しました")
    End Sub

    Private Sub OnCancelEdit(sender As Object, e As EventArgs)
        If _lstMacros.SelectedItem Is Nothing Then
            ClearEditor()
            Return
        End If
        Dim selected = DirectCast(_lstMacros.SelectedItem, MacroItem)
        _current = selected.Clone()
        BindEditor(_current)
        SetStatus("編集を破棄しました")
    End Sub

    Private Sub OnTest(sender As Object, e As EventArgs)
        If _current Is Nothing Then Return
        SyncCurrentFromEditor()

        Dim msg = $"テスト実行します。{Environment.NewLine}" &
                  $"起動前ウェイト {_current.DelaySec:0.##} 秒の間に、入力先ウィンドウをアクティブにしてください。"
        If MessageBox.Show(Me, msg, "テスト実行", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) <> DialogResult.OK Then
            Return
        End If

        Try
            Me.WindowState = FormWindowState.Minimized
            Application.DoEvents()
            KeySender.ExecuteMacro(_current)
            SetStatus("テスト実行完了")
        Catch ex As Exception
            ErrorLogger.Write(ex, "テスト実行")
            MessageBox.Show(Me, $"実行中にエラーが発生しました。{Environment.NewLine}{ex.Message}",
                            "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            Me.WindowState = FormWindowState.Normal
            Me.Activate()
        End Try
    End Sub

    Private Sub PersistAll()
        Try
            ConfigStore.Save(_macros)
        Catch ex As Exception
            ErrorLogger.Write(ex, "保存失敗")
            MessageBox.Show(Me, $"保存に失敗しました。{Environment.NewLine}{ex.Message}",
                            "保存エラー", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub SetStatus(text As String)
        _lblStatus.Text = text
    End Sub
End Class
