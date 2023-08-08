// See https://aka.ms/new-console-template for more information
using Microsoft.TeamFoundation.Core.WebApi;

internal class ProjectDetails : ProjecDetails
{

    public ProjectDetails(string project, int workItemCount, int totalWorkItemsCount, int totalBuilds, int repoCount, int testPlansCount)
    {
        this.ProjectName = project;
        WorkItemCount = workItemCount;
        this.TotalWorkItemCount = totalWorkItemsCount;
        this.BuildCount = totalBuilds;
        RepoCount = repoCount;
        TestPlansCount = testPlansCount;
    }
}