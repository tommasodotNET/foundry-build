import { FoundryModels, type FoundryModel, createBuilder } from './.modules/aspire.js';

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

const webResearcherAgent = await project.addPromptAgent(chat, 'web-researcher', {
    instructions: [
        'You are a generic web research specialist used as a tool by another agent.',
        'Use web search only to answer the specific external-facts question you were given.',
        'Return concise findings with source-aware context. Do not make planning decisions for the user.',
        'If the request does not need current or external information, say that web research is not needed.',
    ].join(' '),
})
    .withTool(webSearch);

const tripAgent = await builder.addPythonApp('trip-readiness-agent', './trip-agent-python', 'trip_agent_python/main.py')
    .withUv()
    .withReference(project).waitFor(project)
    .withReference(chat).waitFor(chat)
    .withReference(webResearcherAgent).waitFor(webResearcherAgent)
    .publishAsHostedAgent({project});

await builder.addViteApp('frontend', './frontend')
    .withNpm()
    .withReference(tripAgent).waitFor(tripAgent);

await builder.build().run();