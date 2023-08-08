// See https://aka.ms/new-console-template for more information
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Common;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

//class to hold project details

var PAT = "";
var orgName = "";
var olderThanXMonths = "0";
//ask user to enter org url
Console.WriteLine("Enter your PAT");
PAT = Console.ReadLine();
Console.WriteLine("Enter your Org Name");
orgName = Console.ReadLine();
Console.WriteLine("Show projects with updates later than x Months (eg 1,2,3)");
olderThanXMonths = Console.ReadLine();

var projects = await GetProjects(PAT, orgName, DateTime.Now.AddMonths(-int.Parse(olderThanXMonths)));

//write out projects resuls to csv file
var csv = new StringBuilder();
csv.AppendLine("Project Name,Work Items Updated,Total Work Items,Builds,Repo Updates,Test Plans updates");

foreach (var project in projects)
{
    csv.AppendLine($"{project.ProjectName},{project.WorkItemCount},{project.TotalWorkItemCount},{project.BuildCount},{project.RepoCount},{project.TestPlanCount}");
    Console.WriteLine($"{project.ProjectName}");
}
csv.AppendLine(
    $"Total Projects : {projects.Count}");
File.WriteAllText("projects.csv", csv.ToString());



//method to get all projects from azure devops
static async Task<List<ProjecDetails>> GetProjects(string pat, string orgName, DateTime dateTime)
{
    var activeProjects = new List<ProjecDetails>();
    try
    {

        var client = await GetProjectClient(pat, orgName);
        var projects = await client.GetProjects();
        //loop through projects
        foreach (var project in projects.Where(p => p.State != ProjectState.Deleted))
        {
            //check work items
            var workItemCount = await GetProjectWorkItems(pat, orgName, project.Name, dateTime);
            var totalWorkItemsCount = await GetTotalWorkItems(pat, orgName, project.Name);
            var totalBuilds = await GetProjectBuilds(pat, orgName, project.Name, dateTime);
            var repoCount = await GetProjectRepos(pat, orgName, project.Name, dateTime);
            var testPlansCount = await GetProjectTestPlans(pat, orgName, project.Name, dateTime);
            if(totalBuilds > 0 || workItemCount > 0 || repoCount > 0 || totalWorkItemsCount > 0)
            {
                //create projectdetails object
                var projectDetails = new ProjectDetails(project.Name, workItemCount, totalWorkItemsCount, totalBuilds, repoCount, testPlansCount);
                activeProjects.Add(projectDetails);
                
            }
           

        }
    }catch(Exception ex)
    {
        Console.WriteLine(ex.Message);
    }

    return activeProjects;
}

//method to get project testplans
static async Task<int> GetProjectTestPlans(string pat, string orgName, string projectname, DateTime dateTime)
{
    var testClient = await GetTestClient(pat, orgName);
    var testRuns = await testClient.GetTestRunsAsync(projectname);
    var testPlanCount = 0;
    foreach (var testPlan in testRuns)
    {
        //get last update date for test plan
        if (testPlan.CompletedDate >= dateTime)
            testPlanCount++;
    }
    return testPlanCount;
}

//method to get project work items
static async Task<int> GetProjectWorkItems(string pat, string orgName, string projectname, DateTime dateTime)
{
    var workItemClient = await GetWorkItemClient(pat, orgName);
    var workItems = new List<WorkItem>();
    string iso8601 = dateTime.ToString("MM/dd/yy");
    var wiql = new Wiql()
    {
        Query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{projectname}' AND [System.ChangedDate] > '{iso8601}' ORDER BY [System.ChangedDate] DESC"
 
    };
    var result = await workItemClient.QueryByWiqlAsync(wiql).ConfigureAwait(false);
    //if work item update date older than 2 months then add to list
    var ids = result.WorkItems.Select(item => item.Id).Count();

    return ids;
}

static async Task<int> GetTotalWorkItems(string pat, string orgName, string projectname)
{
    var workItemClient = await GetWorkItemClient(pat, orgName);
    var workItems = new List<WorkItem>();
    var wiql = new Wiql()
    {
        Query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{projectname}' ORDER BY [System.ChangedDate] DESC"
 
    };
    var result = await workItemClient.QueryByWiqlAsync(wiql).ConfigureAwait(false);
    //if work item update date older than 2 months then add to list
    var ids = result.WorkItems.Select(item => item.Id).Count();

    return ids;
}

//get project repositories
static async Task<int> GetProjectRepos(string pat, string orgName, string projectname, DateTime dateTime)
{
    var gitClient = await GetGitClient(pat, orgName);
    var repos = await gitClient.GetRepositoriesAsync(projectname);
    var repoCount = 0;
    foreach (var repo in repos)
    {
        //get last update date for repository
        if (repo.ProjectReference.LastUpdateTime >= dateTime)
            repoCount++;
    }
    return repoCount;
}


//get project Builds
static async Task<int> GetProjectBuilds(string pat, string orgName, string projectname, DateTime dateTime)
{
    var buildClient = await GetBuildClient(pat, orgName);
    var builds = await buildClient.GetBuildsAsync(projectname);
    return builds.Where(x => x.LastChangedDate >= dateTime).Count();
    
}



//this is project settings updates not work items or repos
static async Task<List<TeamProjectReference>> GetProjectsWithUpdateInlast2Months(string pat, string orgName)
{
    var client = await GetProjectClient(pat, orgName);
    var projects = await client.GetProjects();

    return projects.Where(p => p.LastUpdateTime > DateTime.Now.AddMonths(-2) && p.State != ProjectState.Deleted).ToList();
}

//method to get GetBuildClient
static async Task<BuildHttpClient> GetBuildClient(string pat, string orgName)
{
    // create a connection
    VssConnection connection = new VssConnection(new Uri($"https://dev.azure.com/{orgName}"), new VssBasicCredential(string.Empty, pat));

    // create client
    BuildHttpClient buildClient = connection.GetClient<BuildHttpClient>();

    return buildClient;
}




// method to create and get client for azure devops services
static async Task<GitHttpClient> GetGitClient(string pat, string orgName)
{
    // create a connection
    VssConnection connection = new VssConnection(new Uri($"https://dev.azure.com/{orgName}"), new VssBasicCredential(string.Empty, pat));

    // create client
    GitHttpClient gitClient = connection.GetClient<GitHttpClient>();

    return gitClient;
}

static async Task<ProjectHttpClient> GetProjectClient(string pat, string orgName)
{

    // create a connection
    VssConnection connection = new VssConnection(new Uri($"https://dev.azure.com/{orgName}"), new VssBasicCredential(string.Empty, pat));

    // create client
    ProjectHttpClient projectClient = connection.GetClient<ProjectHttpClient>();

    return projectClient;
}

//create testitemsclient method
static async Task<TestManagementHttpClient> GetTestClient(string pat, string orgName)
{
    // create a connection
    VssConnection connection = new VssConnection(new Uri($"https://dev.azure.com/{orgName}"), new VssBasicCredential(string.Empty, pat));

    // create client
    TestManagementHttpClient testClient = connection.GetClient<TestManagementHttpClient>();

    return testClient;
}

//create GetWorkItemClient method
static async Task<WorkItemTrackingHttpClient> GetWorkItemClient(string pat, string orgName)
{
    // create a connection
    VssConnection connection = new VssConnection(new Uri($"https://dev.azure.com/{orgName}"), new VssBasicCredential(string.Empty, pat));

    // create client
    WorkItemTrackingHttpClient workItemClient = connection.GetClient<WorkItemTrackingHttpClient>();

    return workItemClient;
}


internal class ProjecDetails
{
    public string ProjectName { get; set; }
    public int WorkItemCount { get; set; }
    public int BuildCount { get; set; }
    public int RepoCount { get; set; }
    public int TestPlanCount { get; set; }
    public int TotalWorkItemCount { get; set; }
    public int TestPlansCount { get; set; }

    //constructor
    // public ProjecDetails(string projectName, int workItemCount, int buildCount, int repoCount, int testPlanCount, int totalWorkItemCount, int testPlansCount){
    //     ProjectName = projectName;
    //     WorkItemCount = workItemCount;
    //     BuildCount = buildCount;
    //     RepoCount = repoCount;
    //     TestPlanCount = testPlanCount;
    //     TotalWorkItemCount = totalWorkItemCount;
    //     TestPlansCount = testPlansCount;
    // }
}


