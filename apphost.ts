import { AzureContainerRegistryRole, FoundryModels, type FoundryModel, createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

const foundry = await builder.addFoundry('tsfbuild');

const project = await foundry.addProject('tsfproject');

const model: FoundryModel = FoundryModels.OpenAI.Gpt41Mini;
const chat = await foundry
    .addDeployment('chat', model)
    .withProperties(async (deployment) => {
        await deployment.deploymentName.set('chat-deployment');
        await deployment.skuCapacity.set(10);
        const _capacity: number = await deployment.skuCapacity.get();
    });

const webSearch = await project.addWebSearchTool('web-search');

const webSearcherAgent = await project.addPromptAgent(chat, 'web-searcher')
    .withTool(webSearch);

const weatherAgent = await builder.addPythonApp("weather-agent-python", "./weather-agent-python", "weather_agent_python/main.py")
    .withUv()
    .withReference(project).waitFor(project)
    .withReference(chat).waitFor(chat)
    .withReference(webSearcherAgent).waitFor(webSearcherAgent)
    .publishAsHostedAgent({project});

await builder.build().run();