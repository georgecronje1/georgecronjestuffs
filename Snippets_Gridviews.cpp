//Add Multiple header rows (normally called from "OnRowCreate"
 if (e.Row.RowType == DataControlRowType.Header)
    {
   	 GridView HeaderGrid = (GridView)sender;
   	 GridViewRow HeaderGridRow = new GridViewRow(0, 0, DataControlRowType.Header, DataControlRowState.Insert);
   	 TableCell HeaderCell = new TableCell();
   	 HeaderCell.Text = "Department";
   	 HeaderCell.ColumnSpan = 2;
   	 HeaderGridRow.Cells.Add(HeaderCell);

   	 HeaderCell = new TableCell();
   	 HeaderCell.Text = "Employee";
   	 HeaderCell.ColumnSpan = 2;
   	 HeaderGridRow.Cells.Add(HeaderCell);

   	 grvMergeHeader.Controls[0].Controls.AddAt(0, HeaderGridRow);
    }
//Or to add another Footer row
private void createnewrow(GridView gv)
	{
    	TableCell confirmLabelCell = new TableCell();
    	confirmLabelCell.ColumnSpan = gv.Columns.Count;
    	confirmLabelCell.Text = "footer row";
   	 
    	Label confirmLabel = new Label();
    	confirmLabel.ForeColor = System.Drawing.Color.Red;
    	confirmLabel.Text = "WARNING! This will delete all items in the list! Are you sure?";
   	 
    	confirmLabelCell.Controls.Add(confirmLabel);
   	 
    	GridViewRow confirmLabelRow = new GridViewRow(0, 0, DataControlRowType.Footer, DataControlRowState.Normal);
    	confirmLabelRow.Cells.Add(confirmLabelCell);

    	gv.Controls[0].Controls.Add(confirmLabelRow);
	}
// Access DataKeys for GridView
<asp:GridView ID="GridViewIsShippedWith" runat="server" AllowPaging="False"
				AutoGenerateColumns="false" DataKeyNames="id"
				ShowFooter="true"
				EmptyDataText="No parts are currently linked with this."
				onrowdatabound="GridViewIsShippedWith_RowDataBound"
				onrowdeleting="GridViewIsShippedWith_RowDeleting"
				onrowcommand="GridViewIsShippedWith_RowCommand">
	<Columns>
		<asp:TemplateField HeaderText="Delete">
			<ItemTemplate>
				<asp:LinkButton ID="delete" runat="server" CommandName="Delete" CommandArgument='<%# Container.DataItemIndex %>' Text="Delete"></asp:LinkButton>
			</ItemTemplate>
			<FooterTemplate>
// C#
DataKeyArray gvDks = gv.DataKeys;
    	foreach (DataKey item in gvDks)
    	{
        	ids_to_delete.Add(Int32.Parse(item.Value.ToString()));
    	}

protected void GridViewShipsWith_RowDeleting(object sender, GridViewDeleteEventArgs e)
	{
    	callDeleteProc("delete_ships_with", GridViewShipsWith.DataKeys[e.RowIndex].Value, newShipsWithLabel, false, "single");
	}