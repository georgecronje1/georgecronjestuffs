public enum session_names_enum
        {
            selected_environment = 0,
            selected_client = 1,
            selected_survey = 2,
            main_text_surveyItems = 3
        }

private void setup_gridview_with_sql(GridView gv, string sql, session_names_enum session_names_enum)
	{
		DataTable table = execute_sql_return_dt(sql);
		set_session_value(table, session_names_enum);
		gv.DataSource = table;
		gv.DataBind();
	}

public void set_session_value(Object ob, session_names_enum session_name)
	{
		try
		{
			Session[session_name.ToString()] = ob;
		}
		catch (Exception ex)
		{
			display_error_expection(ex, "Error while setting Session variable", session_name.ToString());
		}
	}

public object get_session_value(session_names_enum session_name)
{
	try
	{
		Object temp = Session[session_name.ToString()];
		return temp;
	}
	catch (Exception ex)
	{
		display_error_expection(ex, "Error while getting Session variable", session_name.ToString());
		return default(Object);
	}
}

public DataTable filter_table_by_single_column(DataTable table_to_filter, string column, string value, string filter_operator)
{
	string filter_expression = column + filter_operator + "'" + value + "'";
	DataView dv = new DataView(table_to_filter);
	dv.RowFilter = filter_expression;
	return dv.ToTable();
}

public void setup_ddl_with_query(DropDownList ddl, string sql, string value, string text)
{
	try
	{
		DataTable table = execute_sql_return_dt(sql);
		setup_ddl_with(ddl, table, value, text);
	}
	catch (Exception ex)
	{
		display_error_expection(ex, "Error setting up dropdownlist with query", (ddl.ID.ToString() + " " + sql));
	}
}

public void setup_ddl_with(DropDownList ddl_to_setup, DataTable dataTable, string ddl_value, string ddl_text)
{
	try
	{
		ddl_to_setup.DataSource = dataTable;
		ddl_to_setup.DataValueField = dataTable.Columns[ddl_value].ToString();
		ddl_to_setup.DataTextField = dataTable.Columns[ddl_text].ToString();
		ddl_to_setup.DataBind();
	}
	catch (Exception ex)
	{
		display_error_expection(ex, "Exception Thrown during setup of DropDown list", ddl_to_setup.ID.ToString());
	}
}

public string get_ddl_selected_value(DropDownList ddl)
{
	try
	{
		string selected_value = ddl.SelectedValue.ToString();

		return selected_value;
	}
	catch (Exception ex)
	{
		display_error_expection(ex, "Error getting value from Dropdown List", ddl.ID.ToString());
		return default(string);
	}
}

public string get_ddl_selected_text(DropDownList ddl)
{
	try
	{
		string selected_text = ddl.SelectedItem.Text.ToString();

		return selected_text;
	}
	catch (Exception ex)
	{
		display_error_expection(ex, "Error getting text from Dropdown List", ddl.ID.ToString());
		return default(string);
	}
}

public DataTable execute_sql_return_dt(string sql)
{
	DataSet ds = new DataSet();

	string connectionString = (string)get_session_value(session_names_enum.selected_environment);
	try
	{
		using (SqlConnection sqlcon = new SqlConnection(System.Configuration.ConfigurationManager.ConnectionStrings[connectionString].ToString()))
		{
			sqlcon.Open();
			SqlDataAdapter da = new SqlDataAdapter(sql, sqlcon);
			da.Fill(ds);
			sqlcon.Close();
		}
		DataTable dt = ds.Tables[0];
		return dt;
	}
	catch (Exception ex)
	{
		display_error_expection(ex, "Error while executing SQL DataTable return statement", sql);
		return default(DataTable);
	}
}

public DataSet execute_sql_return_ds(string sql)
{
	DataSet ds = new DataSet();
	string connectionString = (string)get_session_value(session_names_enum.selected_environment);
	try
	{
		using (SqlConnection sqlcon = new SqlConnection(System.Configuration.ConfigurationManager.ConnectionStrings[connectionString].ToString()))
			{
				sqlcon.Open();
				SqlDataAdapter da = new SqlDataAdapter(sql, sqlcon);
				da.Fill(ds);
				sqlcon.Close();
			}
		return ds;
	}
	catch (Exception ex)
	{
		display_error_expection(ex, "Error while executing SQL DataSet return statement", sql);
		return default(DataSet);
	}
}

public void display_error_expection(Exception ex, string error_message, string item_identifier)
{
	error_message_lbl.Text = error_message.ToString() + " " + item_identifier;
	exception_message_lbl.Text = ex.InnerException == null ? "" : ex.InnerException.ToString() + 
									ex.Message == null ? "" : ex.Message.ToString() + 
									ex.Source == null ? "" : ex.Source.ToString() + 
									ex.StackTrace == null ? "" : ex.StackTrace.ToString() +
									ex.TargetSite == null ? "" : ex.TargetSite.ToString();
}

public int GetColumnIndexByHeaderText(GridView grid, string name)
	{
    	int index = -1;
    	for (int i = 0; i < grid.Columns.Count; i++)
    	{
        	if (grid.Columns[i].HeaderText.ToLower().Trim() == name.ToLower().Trim())
        	{
            	index = i;
            	break;
        	}
    	}
    	return index;
	}