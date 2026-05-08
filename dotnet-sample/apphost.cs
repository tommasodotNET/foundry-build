#:sdk Aspire.AppHost.Sdk@13.4.0-pr.16846.gc2cc6711
#:package Aspire.Hosting.Foundry@13.4.0-pr.16846.gc2cc6711
#:package Aspire.Hosting.JavaScript@13.4.0-pr.16846.gc2cc6711

#:project ./trip-agent-dotnet/TripAgent.Dotnet.csproj

using Aspire.Hosting.Foundry;

var builder = DistributedApplication.CreateBuilder(args);

var foundry = builder.AddFoundry("csfoundrybuild");

var project = foundry.AddProject("csprojectbuild");

var chat = project
    .AddModelDeployment("chat", FoundryModel.OpenAI.Gpt41)
    .WithProperties(configure => { configure.SkuCapacity = 10; configure.SkuName = "GlobalStandard"; });

var webSearch = project.AddWebSearchTool("web-search");

var webResearcherAgent = project
    .AddPromptAgent(
        chat,
        name: "webresearcher",
        instructions: "You are a generic web research specialist used as a tool by another agent. Use web search only to answer the specific external-facts question you were given. Return concise findings with source-aware context. Do not make planning decisions for the user. If the request does not need current or external information, say that web research is not needed.")
    .WithTool(webSearch);

var tripAgent = builder
    .AddProject<Projects.TripAgent_Dotnet>("trip-readiness-agent")
    .WithReference(project).WaitFor(project)
    .WithReference(chat).WaitFor(chat)
    .WithReference(webResearcherAgent).WaitFor(webResearcherAgent)
    .PublishAsHostedAgent(project);

if (builder.ExecutionContext.IsRunMode)
{
    builder
        .AddViteApp("frontend", "./frontend", "dev")
        .WithReference(tripAgent).WaitFor(tripAgent);
}

builder.Build().Run();