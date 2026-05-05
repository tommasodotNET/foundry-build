using System.ComponentModel;
using System.Data.Common;
using System.Text.Json;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;

#pragma warning disable OPENAI001

var port = Environment.GetEnvironmentVariable("DEFAULT_AD_PORT") ?? "8088";
var projectConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__tsfproject")
    ?? throw new InvalidOperationException("ConnectionStrings__tsfproject is not set.");
var chatConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__chat")
    ?? throw new InvalidOperationException("ConnectionStrings__chat is not set.");

var projectConnectionBuilder = new DbConnectionStringBuilder { ConnectionString = projectConnectionString };
var chatConnectionBuilder = new DbConnectionStringBuilder { ConnectionString = chatConnectionString };

var projectEndpoint = GetRequiredConnectionValue(projectConnectionBuilder, "Endpoint");
var deploymentName = GetRequiredConnectionValue(chatConnectionBuilder, "Deployment");

if (!Uri.TryCreate(projectEndpoint, UriKind.Absolute, out var projectUri) || projectUri is null)
{
    throw new InvalidOperationException("ConnectionStrings__tsfproject contains an invalid Endpoint value.");
}

var credential = new DefaultAzureCredential();
var foundryProjectClient = new AIProjectClient(projectUri, credential);

var webResearcherAgentName = Environment.GetEnvironmentVariable("WEB_RESEARCHER_AGENTNAME")
    ?? throw new InvalidOperationException("WEB_RESEARCHER_AGENTNAME is not set.");
var webResearcherAgentReference = new AgentReference(name: webResearcherAgentName);
var webResearcherAgent = foundryProjectClient.ProjectOpenAIClient
    .GetProjectResponsesClientForAgent(webResearcherAgentReference)
    .AsIChatClient(deploymentName)
    .AsAIAgent(webResearcherAgentName, description: "Searches the web for current trip and destination context.");

var agent = foundryProjectClient
    .GetProjectOpenAIClient()
    .GetProjectResponsesClient()
    .AsIChatClient(deploymentName)
    .AsBuilder()
    .ConfigureOptions(options => options.AllowMultipleToolCalls = true)
    .UseOpenTelemetry(sourceName: "Foundry.Agents", configure: options => options.EnableSensitiveData = true)
    .Build()
    .AsAIAgent(
        name: "trip-readiness-agent",
        instructions: "You are a Trip Readiness Assistant. Use the trip profile tool first for the traveler's private plan, preferences, constraints, and checklist. Help the user understand what is ready, what is missing, and what to do next. Use the web researcher only when the user asks for current external context such as entry rules, travel advisories, local events, closures, transit disruption, weather, opening hours, or source-backed recommendations. Do not call the web researcher for questions that can be answered from the private trip profile.",
        tools:
        [
            AIFunctionFactory.Create(GetTripProfile),
            webResearcherAgent.AsAIFunction()
        ]);

var functionInvokingChatClient = agent.GetService<FunctionInvokingChatClient>();
if (functionInvokingChatClient is not null)
{
    functionInvokingChatClient.AllowConcurrentInvocation = true;
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://+:{port}");

builder.AddServiceDefaults();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});
builder.Services.AddFoundryResponses(agent);

var app = builder.Build();

app.UseCors();
app.MapFoundryResponses();
app.MapGet("/liveness", () => Results.Ok(new { status = "healthy", service = "trip-readiness-agent" }));
app.MapGet("/readiness", () => Results.Ok(new { status = "ready", service = "trip-readiness-agent" }));
app.MapDefaultEndpoints();
app.Run();

[Description("Get the traveler's private trip plan, preferences, and readiness checklist")]
static string GetTripProfile()
{
    var tripProfile = new
    {
        traveler = new
        {
            name = "Avery",
            homeAirport = "SEA",
            travelStyle = "light packing, walkable neighborhoods, local food",
            accessibilityNeeds = "none specified"
        },
        trip = new
        {
            destination = "Lisbon, Portugal",
            dates = new
            {
                departure = "2026-06-14",
                @return = "2026-06-21"
            },
            purpose = "one-week city break with a few remote-work mornings",
            lodging = "Baixa-Chiado apartment, check-in after 15:00"
        },
        constraints = new
        {
            budgetUsd = 2400,
            carryOnOnly = true,
            remoteWorkMornings = new[] { "2026-06-16", "2026-06-18" },
            dietaryPreferences = new[] { "vegetarian-friendly options" }
        },
        currentPlan = new
        {
            booked = new[] { "round-trip flights", "lodging", "airport transfer on arrival" },
            unconfirmed = new[] { "travel insurance", "phone roaming plan", "restaurant reservations" },
            draftItinerary = new[]
            {
                "Alfama walk",
                "Belem museums",
                "Sintra day trip",
                "LX Factory evening"
            }
        },
        packingChecklist = new
        {
            done = new[] { "passport", "USB-C charger", "walking shoes" },
            stillNeeded = new[] { "power adapter", "light rain layer", "printed lodging address" }
        }
    };

    return JsonSerializer.Serialize(tripProfile, new JsonSerializerOptions { WriteIndented = true });
}

static string GetRequiredConnectionValue(DbConnectionStringBuilder connectionBuilder, string key)
{
    if (!connectionBuilder.TryGetValue(key, out var rawValue) || rawValue is null)
    {
        throw new InvalidOperationException($"Connection string is missing '{key}'.");
    }

    var value = rawValue.ToString();
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Connection string has an empty '{key}' value.");
    }

    return value;
}