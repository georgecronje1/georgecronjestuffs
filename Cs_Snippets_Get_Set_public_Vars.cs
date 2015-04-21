private int CompleteType
    {
        get 
        { 
            if (Session["Watch_CompleteType"] == null) 
                this.CompleteType = 0;
            return Convert.ToInt32(Session["Watch_CompleteType"]);
        }
        set { Session["Watch_CompleteType"] = value; }
    }
    private int ConclusionItemId
    {
        get
        {
            if (Session["Watch_ConclusionItemId"] == null)
                this.ConclusionItemId = 0;
            return Convert.ToInt32(Session["Watch_ConclusionItemId"]);
        }
        set { Session["Watch_ConclusionItemId"] = value; }
    }
    private int PageIndex
    {
        get
        {
            if (Session["Watch_PageIndex"] == null)
                this.PageIndex = 0;
            return Convert.ToInt32(Session["Watch_PageIndex"]);
        }
        set { Session["Watch_PageIndex"] = value; }
    }