-----------------------------------------------------------------------------------------------
-- SUPPORT ONLY SUPPORT ONLY SUPPORT ONLY SUPPORT ONLY SUPPORT ONLY SUPPORT ONLY SUPPORT ONLY
-----------------------------------------------------------------------------------------------

-----------------------------------------------------------------------------------------------
-- Script Context:
-----------------------------------------------------------------------------------------------
declare @ScriptReference nvarchar(max) = 'PBI1520';		-- PBI/Bug, e.g. "Bug1234"
declare @ScriptDescription nvarchar(max) = 'Data fixes for Acurity';
declare @ScriptCreatedBy nvarchar(250) = 'Faisal Noor, David Clements'; 
declare @ScriptCreatedDate datetime2 = getdate(); 	--'yyyy-MM-dd HH:mm'

-----------------------------------------------------------------------------------------------
-- Version History (Previous)  -- select * from __VersionHistory order by Major desc, Minor desc, Build desc, Revision desc, Patch desc
-----------------------------------------------------------------------------------------------
declare @PreviousVerHist_Major int = 4;
declare @PreviousVerHist_Minor int = 10;
declare @PreviousVerHist_Build int = 16;	-- sprint
declare @PreviousVerHist_Patch int = 0;		-- support script version

-----------------------------------------------------------------------------------------------
-- Version History (Current)
-----------------------------------------------------------------------------------------------
declare @CurrentVerHist_Major int = 4;
declare @CurrentVerHist_Minor int = 10;
declare @CurrentVerHist_Build int = 16;		-- sprint
declare @CurrentVerHist_Patch int = 1;		-- support script version
-----------------------------------------------------------------------------------------------

declare @DebugFlag bit = 'false';

begin transaction

begin try

	------------------------
	-- Check Version History
	------------------------
	if (not exists (select 1 from __VersionHistory where Major = @PreviousVerHist_Major and Minor = @PreviousVerHist_Minor and Build = @PreviousVerHist_Build and Revision is null and Patch = @PreviousVerHist_Patch))
		throw 99000, 'Cannot continue; the previous Version History does not match.', 1;

	if (exists (select 1 from __VersionHistory where Major = @CurrentVerHist_Major and Minor = @CurrentVerHist_Minor and Build = @CurrentVerHist_Build and Revision is null and Patch = @CurrentVerHist_Patch))
		throw 99001, 'Cannot continue; this support script already has a record in Version History.', 1;

	----------------------
	-- Start of custom SQL
	----------------------

	-- Delete duplicate AnswerActives for Response 2810600
	;with my_cte (AnswerId, ResponseId, SurveyItemId, QOptionId, SubPartId, AnsString, RowNumber) as
	(
	Select aa.AnswerId, aa.ResponseId, aa.SurveyItemId, aa.QOptionId, aa.SubPartId, aa.AnsString, Row_Number() OVER (PARTITION BY aa.SurveyItemId, aa.QOptionId, aa.SubPartId, aa.AnsString ORDER BY aa.SurveyItemId, aa.QOptionId, aa.SubPartId, aa.AnsString)
	from AnswerActive aa 
	Inner Join SurveyItem si on aa.SurveyitemID = si.SurveyitemID
	where aa.ResponseID = 2810600 
	And si.BranchID <> 0 
	)
	delete aa 
	from my_cte m
	inner join AnswerActive aa on m.Answerid = aa.AnswerId
	where m.RowNumber > 1
	;

	-- Delete duplicate Answers for Response 2382332
	;with my_cte (AnswerId, ResponseId, SurveyItemId, QOptionId, SubPartId, AnsString, RowNumber) as
	(
	Select aa.AnswerId, aa.ResponseId, aa.SurveyItemId, aa.QOptionId, aa.SubPartId, aa.AnsString, Row_Number() OVER (PARTITION BY aa.SurveyItemId, aa.QOptionId, aa.SubPartId, aa.AnsString ORDER BY aa.SurveyItemId, aa.QOptionId, aa.SubPartId, aa.AnsString)
	from Answer aa 
	Inner Join SurveyItem si on aa.SurveyitemID = si.SurveyitemID
	where aa.ResponseID = 2382332 
	And si.BranchID <> 0 
	)
	delete aa
	from my_cte m
	inner join Answer aa on m.Answerid = aa.AnswerId
	where m.RowNumber > 1
	;

	----------------------
	-- End of custom SQL
	----------------------

	-------------------------
	-- Create Version History
	-------------------------
	insert into __VersionHistory ([Major], [Minor], [Build], [Revision], [Patch], [Comment], [CreatedBy], [CreatedDate])
		values (@CurrentVerHist_Major, @CurrentVerHist_Minor, @CurrentVerHist_Build, null, @CurrentVerHist_Patch, case when @ScriptReference = '' then @ScriptDescription else concat(@ScriptReference, ' - ', @ScriptDescription) end, @ScriptCreatedBy, @ScriptCreatedDate);

end try
begin catch

	print 'Error has occurred - rolling back';

	select 
        error_number() AS ErrorNumber
        ,error_severity() AS ErrorSeverity
        ,error_state() AS ErrorState
        ,error_procedure() AS ErrorProcedure
        ,error_line() AS ErrorLine
        ,error_message() AS ErrorMessage;

    if @@trancount > 0
	begin
        rollback transaction;
		print 'Transaction has been rolled back!';
	end

end catch

if @@trancount > 0 
begin
	if @DebugFlag = 'true'
	begin
		rollback transaction;
		print 'Transaction has been rolled back!';
	end
	else
	begin
		commit transaction;
	end
end

