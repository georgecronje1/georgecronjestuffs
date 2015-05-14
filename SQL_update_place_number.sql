-----------------------------------------------------------------------------------------------
-- SPRINT RELEASE ONLY SPRINT RELEASE ONLY SPRINT RELEASE ONLY SPRINT RELEASE ONLY SPRINT
-----------------------------------------------------------------------------------------------

-----------------------------------------------------------------------------------------------
-- Script Context:
-----------------------------------------------------------------------------------------------
declare @ScriptReference nvarchar(max) = 'PBI1466'	-- PBI/Bug
declare @ScriptDescription nvarchar(max) = 'Add Overall Value filter to Family Feedback'
declare @ScriptCreatedBy nvarchar(250) = 'George Cronje' 
declare @ScriptCreatedDate datetime2 = 'yyyy-mm-dd hh:mm'

-----------------------------------------------------------------------------------------------
-- Version History (Previous)  -- select * from __VersionHistory order by Major desc, Minor desc, Build desc, Revision desc, Patch desc
-----------------------------------------------------------------------------------------------
declare @PreviousVerHist_Major int = 1;
declare @PreviousVerHist_Minor int = 1;
declare @PreviousVerHist_Build int = 15;	-- sprint
declare @PreviousVerHist_Revision int = 0;	-- script version

-----------------------------------------------------------------------------------------------
-- Version History (Current)
-----------------------------------------------------------------------------------------------
declare @CurrentVerHist_Major int = 1;		-- increments only with major release
declare @CurrentVerHist_Minor int = 1;		-- increments with each sprint
declare @CurrentVerHist_Build int = 15;		-- sprint number
declare @CurrentVerHist_Revision int = 1;	-- script version
-----------------------------------------------------------------------------------------------

declare @DebugFlag bit = 'false';

begin transaction

begin try

	------------------------
	-- Check Version History
	------------------------
	if (not exists (select 1 from __VersionHistory where Major = @PreviousVerHist_Major and Minor = @PreviousVerHist_Minor and Build = @PreviousVerHist_Build and Revision = @PreviousVerHist_Revision and Patch is null))
		throw 99000, 'Cannot continue; the previous Version History does not match.', 1;

	if (exists (select 1 from __VersionHistory where Major = @CurrentVerHist_Major and Minor = @CurrentVerHist_Minor and Build = @CurrentVerHist_Build and Revision = @CurrentVerHist_Revision and Patch is null))
		throw 99001, 'Cannot continue; this script already has a record in Version History.', 1;

	----------------------
	-- Start of custom SQL
	----------------------

	declare		@insert_above_here	int = (select PlaceNo from Filters where SurveyId = 3506 and Label = 'Staff Member')

	update		Filters
	set			PlaceNo = f.PlaceNo + 1
	from		Filters f
	where		f.SurveyId = 3506
	and			f.PlaceNo > @insert_above_here

	insert into	Filters (Label, FilterTypeEnum, PlaceNo, FilterOrderEnum, SurveyItemId, SurveyId, IsBoundaryFilter)
	values				('Overall Value Score', 1, (@insert_above_here + 1), 2, 87867, 3506, 'false')

	declare @OverallFilterId int = scope_identity();

	--select * from Filters where SurveyId = 3506 order by PlaceNo

	insert into FilterReportRoleMapping (FilterId, ReportId, RoleId)
	select		@OverallFilterId as 'FilterId', rep.Id as 'ReportId', rol.Id as 'RoleId' --rep.Id as 'ReportId', rep.ReportName, rol.Id as 'RoleId', rol.Name as 'RoleName'
	from		Categories cat
	join		Pages p on cat.Id = p.CategoryId
	join		Reports rep on p.Id = rep.PageId
	join		Roles rol on cat.ClientId = rol.ClientId
	where		cat.ClientId = 7
	and			cat.IsAdminCategory = 'false'
	and			cat.SurveyId = 3506
	and			rep.ReportName <> 'Email response rate'
	and			rep.ReportName <> 'All responses'
	and			rep.ReportName <> 'Overall value'


	----------------------
	-- End of custom SQL
	----------------------

	-------------------------
	-- Create Version History
	-------------------------
	insert into __VersionHistory ([Major], [Minor], [Build], [Revision], [Patch], [Comment], [CreatedBy], [CreatedDate])
		values (@CurrentVerHist_Major, @CurrentVerHist_Minor, @CurrentVerHist_Build, @CurrentVerHist_Revision, null, @ScriptDescription, @ScriptCreatedBy, @ScriptCreatedDate);

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

