import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Loader2, Plane, RotateCcw, SendHorizontal } from 'lucide-react';
import { sendMessageStream } from './lib/responses-client';

type MessageRole = 'user' | 'assistant';

interface ChatMessage {
  id: string;
  role: MessageRole;
  text: string;
}

const starters = [
  'Am I ready for my Lisbon trip?',
  'What should I confirm before I leave?',
  'Find current events near my destination.',
];

function createMessage(role: MessageRole, text: string): ChatMessage {
  return { id: crypto.randomUUID(), role, text };
}

export default function App() {
  const [messages, setMessages] = useState<ChatMessage[]>([
    createMessage('assistant', 'Ready to review your trip plan.'),
  ]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [contextId, setContextId] = useState<string>();
  const endRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' });
  }, [messages]);

  function resetConversation() {
    setContextId(undefined);
    setInput('');
    setMessages([createMessage('assistant', 'Ready to review your trip plan.')]);
  }

  async function sendMessage(text: string) {
    const prompt = text.trim();
    if (!prompt || loading) {
      return;
    }

    const assistantId = crypto.randomUUID();
    let responseText = '';

    setInput('');
    setLoading(true);
    setMessages((current) => [
      ...current,
      createMessage('user', prompt),
      { id: assistantId, role: 'assistant', text: '' },
    ]);

    try {
      for await (const event of sendMessageStream(prompt, contextId)) {
        if (event.contextId) {
          setContextId(event.contextId);
        }

        if (event.content) {
          responseText += event.content;
          setMessages((current) =>
            current.map((message) =>
              message.id === assistantId ? { ...message, text: responseText } : message,
            ),
          );
        }
      }

      if (!responseText) {
        setMessages((current) =>
          current.map((message) =>
            message.id === assistantId ? { ...message, text: 'No response returned.' } : message,
          ),
        );
      }
    } catch (error) {
      setMessages((current) =>
        current.map((message) =>
          message.id === assistantId
            ? {
                ...message,
                text: error instanceof Error ? error.message : 'The trip readiness agent could not be reached.',
              }
            : message,
        ),
      );
    } finally {
      setLoading(false);
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await sendMessage(input);
  }

  return (
    <main className="app-shell">
      <section className="chat-surface" aria-label="Trip readiness assistant chat">
        <header className="chat-header">
          <div className="agent-mark" aria-hidden="true">
            <Plane size={22} />
          </div>
          <div>
            <h1>Trip Readiness Assistant</h1>
            <p>{contextId ? 'Conversation active' : 'New conversation'}</p>
          </div>
          <button className="icon-button" type="button" onClick={resetConversation} title="New conversation">
            <RotateCcw size={18} />
          </button>
        </header>

        <div className="starter-row" aria-label="Suggested prompts">
          {starters.map((starter) => (
            <button key={starter} type="button" onClick={() => void sendMessage(starter)} disabled={loading}>
              {starter}
            </button>
          ))}
        </div>

        <div className="message-list" aria-live="polite">
          {messages.map((message) => (
            <article key={message.id} className={`message ${message.role}`}>
              <div className="message-meta">{message.role === 'user' ? 'You' : 'Trip Assistant'}</div>
              <div className="message-bubble">
                {message.text || (loading && message.role === 'assistant' ? <Loader2 className="spin" size={18} /> : null)}
              </div>
            </article>
          ))}
          <div ref={endRef} />
        </div>

        <form className="composer" onSubmit={handleSubmit}>
          <input
            value={input}
            onChange={(event) => setInput(event.target.value)}
            placeholder="Ask about readiness, plans, or current destination context"
            disabled={loading}
          />
          <button type="submit" disabled={loading || !input.trim()} title="Send message">
            {loading ? <Loader2 className="spin" size={18} /> : <SendHorizontal size={18} />}
          </button>
        </form>
      </section>
    </main>
  );
}
