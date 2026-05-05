import json
import os

from agent_framework import Agent, tool
from agent_framework.observability import configure_otel_providers
from agent_framework.foundry import FoundryAgent, FoundryChatClient
from agent_framework_foundry_hosting import ResponsesHostServer
from azure.identity import DefaultAzureCredential
from starlette.requests import Request
from starlette.responses import JSONResponse
from starlette.routing import Route


@tool(name="get_trip_profile", description="Get the traveler's private trip plan, preferences, and readiness checklist")
async def get_trip_profile() -> str:
    trip_profile = {
        "traveler": {
            "name": "Avery",
            "homeAirport": "SEA",
            "travelStyle": "light packing, walkable neighborhoods, local food",
            "accessibilityNeeds": "none specified",
        },
        "trip": {
            "destination": "Lisbon, Portugal",
            "dates": {
                "departure": "2026-06-14",
                "return": "2026-06-21",
            },
            "purpose": "one-week city break with a few remote-work mornings",
            "lodging": "Baixa-Chiado apartment, check-in after 15:00",
        },
        "constraints": {
            "budgetUsd": 2400,
            "carryOnOnly": True,
            "remoteWorkMornings": ["2026-06-16", "2026-06-18"],
            "dietaryPreferences": ["vegetarian-friendly options"],
        },
        "currentPlan": {
            "booked": ["round-trip flights", "lodging", "airport transfer on arrival"],
            "unconfirmed": ["travel insurance", "phone roaming plan", "restaurant reservations"],
            "draftItinerary": [
                "Alfama walk",
                "Belém museums",
                "Sintra day trip",
                "LX Factory evening",
            ],
        },
        "packingChecklist": {
            "done": ["passport", "USB-C charger", "walking shoes"],
            "stillNeeded": ["power adapter", "light rain layer", "printed lodging address"],
        },
    }

    return json.dumps(trip_profile, indent=2)


def main() -> None:
    configure_otel_providers(enable_sensitive_data=True)

    project_endpoint = os.environ.get("PROJ_TRIP_URI") or os.environ.get("ConnectionStrings__tsfproject", "")
    deployment_name = os.environ.get("CHAT_MODELNAME", "gpt41")

    if project_endpoint.startswith("Endpoint="):
        project_endpoint = project_endpoint.split("Endpoint=", 1)[1].split(";")[0]

    client = FoundryChatClient(
        project_endpoint=project_endpoint,
        model=deployment_name,
        credential=DefaultAzureCredential(),
    );

    webResearcherAgent = FoundryAgent(
        project_endpoint=os.getenv("WEB_RESEARCHER_PROJECTENDPOINT"),
        agent_name=os.getenv("WEB_RESEARCHER_AGENTNAME"),
        allow_preview=True,
        credential=DefaultAzureCredential(),
    )

    agent = Agent(
        client=client,
        name="trip-readiness-agent",
        instructions=(
            "You are a Trip Readiness Assistant. Use get_trip_profile first for the traveler's private "
            "plan, preferences, constraints, and checklist. Help the user understand what is ready, what "
            "is missing, and what to do next. Use the web researcher only when the user asks for current "
            "external context such as entry rules, travel advisories, local events, closures, transit "
            "disruption, weather, opening hours, or source-backed recommendations. Do not call the web "
            "researcher for questions that can be answered from the private trip profile."
        ),
        tools=[get_trip_profile, webResearcherAgent.as_tool()],
        default_options={"store": False},
    )

    async def liveness(request: Request) -> JSONResponse:
        return JSONResponse({"status": "healthy", "service": "trip-readiness-agent"})

    port = int(os.environ.get("DEFAULT_AD_PORT", "8088"))
    server = ResponsesHostServer(agent, routes=[Route("/liveness", liveness, methods=["GET"])])
    server.run(port=port)


if __name__ == "__main__":
    main()
