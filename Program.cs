// See https://aka.ms/new-console-template for more information
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Common;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

var PAT = "";
var orgName = "";
var olderThanXMonths = "0";
//ask user to enter org url
Console.WriteLine("Enter your PAT");
PAT = Console.ReadLine();
Console.WriteLine("Enter your Org Name");
orgName = Console.ReadLine();
Console.WriteLine("Show projects with updates older than x Months (eg 1,2,3)");
olderThanXMonths = Console.ReadLine();

var projects = await GetProjects(PAT, orgName, DateTime.Now.AddMonths(-int.Parse(olderThanXMonths)));

//write out projects resuls to csv file
var csv = new StringBuilder();
csv.AppendLine("Project Name,URL");
foreach (var project in projects)
{
    csv.AppendLine($"{project.Name},{project.Url}");
    Console.WriteLine($"{project.Name}");
}
csv.AppendLine(
    $"Total Projects,{projects.Count}");
File.WriteAllText("projects.csv", csv.ToString());



foreach (var project in projects)
{
    Console.WriteLine(project.Name);
}


//method to get all projects from azure devops
static async Task<List<TeamProjectReference>> GetProjects(string pat, string orgName, DateTime dateTime)
{
    var activeProjects = new List<TeamProjectReference>();
    try
    {

        var client = await GetProjectClient(pat, orgName);
        var projects = await client.GetProjects();
        //loop through projects
        foreach (var project in projects.Where(p => p.State != ProjectState.Deleted))
        {
            //check work items
            var checkWorkItems = await GetProjectWorkItems(pat, orgName, project.Name, dateTime);
            if (checkWorkItems)
            {
                activeProjects.Add(project);
                continue;
            }

            // check builds
            var checkBuilds = await GetProjectBuilds(pat, orgName, project.Name, dateTime);
            if (checkBuilds)
            {
                activeProjects.Add(project);
                continue;
            }

            // check repos
            var checkRepos = await GetProjectRepos(pat, orgName, project.Name, dateTime);
            if (checkRepos)
            {
                activeProjects.Add(project);
                continue;
            }
        }
    }catch(Exception ex)
    {
        Console.WriteLine(ex.Message);
    }

    return activeProjects;
}

//method to get project work items
static async Task<bool> GetProjectWorkItems(string pat, string orgName, string projectname, DateTime dateTime)
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
    var ids = result.WorkItems.Select(item => item.Id).ToArray();

    return ids.Any();
}

//get project repositories
static async Task<bool> GetProjectRepos(string pat, string orgName, string projectname, DateTime dateTime)
{
    var gitClient = await GetGitClient(pat, orgName);
    var repos = await gitClient.GetRepositoriesAsync(projectname);
    foreach (var repo in repos)
    {
        //get last update date for repository
        if (repo.ProjectReference.LastUpdateTime >= dateTime)
            return true;
    }
    return false;
}


//get project Builds
static async Task<bool> GetProjectBuilds(string pat, string orgName, string projectname, DateTime dateTime)
{
    var buildClient = await GetBuildClient(pat, orgName);
    var builds = await buildClient.GetBuildsAsync(projectname);
    return builds.Any(x => x.LastChangedDate >= dateTime);
    
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


//create GetWorkItemClient method
static async Task<WorkItemTrackingHttpClient> GetWorkItemClient(string pat, string orgName)
{
    // create a connection
    VssConnection connection = new VssConnection(new Uri($"https://dev.azure.com/{orgName}"), new VssBasicCredential(string.Empty, pat));

    // create client
    WorkItemTrackingHttpClient workItemClient = connection.GetClient<WorkItemTrackingHttpClient>();

    return workItemClient;
}
