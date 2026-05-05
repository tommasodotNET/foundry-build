import datetime
import json
import os
import random

from agent_framework import Agent, tool
from agent_framework.observability import configure_otel_providers
from agent_framework.foundry import FoundryAgent, FoundryChatClient
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.identity import DefaultAzureCredential
from starlette.requests import Request
from starlette.responses import JSONResponse
from starlette.routing import Route


@tool(name="get_weather_snapshot", description="Get current resort weather and short range forecast")
async def get_weather_snapshot() -> str:
    summaries = ["clear", "light snow", "windy", "low visibility", "bluebird", "storm front building"]
    now = datetime.datetime.now(datetime.UTC)

    snapshot = {
        "observedAt": now.isoformat(),
        "baseTemperatureC": random.randint(-8, 4),
        "summitTemperatureC": random.randint(-15, -2),
        "windKph": random.randint(5, 55),
        "visibility": random.choice(["excellent", "good", "variable", "poor"]),
        "snowNext6HoursCm": random.randint(0, 18),
        "summary": random.choice(summaries),
    }

    return json.dumps(snapshot, indent=2)


def main() -> None:
    configure_otel_providers(enable_sensitive_data=True)

    project_endpoint = os.environ.get("PROJ_WEATHER_URI") or os.environ.get("ConnectionStrings__tsfproject", "")
    deployment_name = os.environ.get("CHAT_MODELNAME", "gpt41")

    if project_endpoint.startswith("Endpoint="):
        project_endpoint = project_endpoint.split("Endpoint=", 1)[1].split(";")[0]

    client = FoundryChatClient(
        project_endpoint=project_endpoint,
        model=deployment_name,
        credential=DefaultAzureCredential(),
    );

    webSearcherAgent = FoundryAgent(
        project_endpoint=os.getenv("WEB_SEARCHER_PROJECTENDPOINT"),
        agent_name=os.getenv("WEB_SEARCHER_AGENTNAME"),
        allow_preview=True,
        credential=DefaultAzureCredential(),
    )

    agent = Agent(
        client=client,
        name="weather-agent-python",
        instructions=(
            "You are the Python Weather Agent. Answer with resort weather data first, "
            "then explain operational impact for guests, outdoor operations, and safety teams."
        ),
        tools=[get_weather_snapshot, webSearcherAgent.as_tool()],
        default_options={"store": False},
    )

    async def liveness(request: Request) -> JSONResponse:
        return JSONResponse({"status": "healthy", "service": "weather-agent-python"})

    port = int(os.environ.get("DEFAULT_AD_PORT", "8088"))
    server = ResponsesHostServer(agent, routes=[Route("/liveness", liveness, methods=["GET"])])
    server.run(port=port)


if __name__ == "__main__":
    main()
