#:property TargetFramework=net10.0
#:property OutputPath=./cookbook.net/artifacts
#:project ../pocketflow.net/pocketflow.net.csproj

using PocketFlow;
using System.Collections.Concurrent;

using TShared = System.Collections.Generic.IDictionary<string, object>;

class StreamNode : Node<TShared, (ConcurrentQueue<string> Chunks, ManualResetEventSlim InterruptEvent, Thread ListenerThread)?, (ManualResetEventSlim InterruptEvent, Thread ListenerThread)>
{
    public override Task<(ConcurrentQueue<string>, ManualResetEventSlim, Thread)?> Prep(TShared shared)
    {
        var interruptEvent = new ManualResetEventSlim(false);
        
        var listenerThread = new Thread(() =>
        {
            Console.WriteLine("Press ENTER at any time to interrupt streaming...\n");
            Console.ReadLine();
            interruptEvent.Set();
        });
        listenerThread.Start();
        
        var prompt = shared.TryGetValue("prompt", out var p) ? p as string ?? "" : "";
        var chunks = MockStreamLLM(prompt);
        
        return Task.FromResult<(ConcurrentQueue<string>, ManualResetEventSlim, Thread)?>((chunks, interruptEvent, listenerThread));
    }

    public override Task<(ManualResetEventSlim InterruptEvent, Thread ListenerThread)> Exec((ConcurrentQueue<string> Chunks, ManualResetEventSlim InterruptEvent, Thread ListenerThread)? inputs)
    {
        if (inputs == null)
            return Task.FromResult<(ManualResetEventSlim InterruptEvent, Thread ListenerThread)>((null!, null!));
        
        var (Chunks, InterruptEvent, ListenerThread) = inputs.Value;
        
        if (Chunks != null)
        {
            while (Chunks.TryDequeue(out var chunk))
            {
                if (InterruptEvent.IsSet)
                {
                    Console.WriteLine("\nUser interrupted streaming.");
                    break;
                }
                
                Console.Write(chunk);
                Thread.Sleep(100);
            }
        }
        
        return Task.FromResult<(ManualResetEventSlim InterruptEvent, Thread ListenerThread)>((InterruptEvent, ListenerThread));
    }

    public override Task<string?> Post(TShared shared, (ConcurrentQueue<string> Chunks, ManualResetEventSlim InterruptEvent, Thread ListenerThread)? prepRes, (ManualResetEventSlim InterruptEvent, Thread ListenerThread) execRes)
    {
        execRes.InterruptEvent?.Set();
        execRes.ListenerThread?.Join();
        return Task.FromResult<string?>("default");
    }

    private ConcurrentQueue<string> MockStreamLLM(string prompt)
    {
        var chunks = new ConcurrentQueue<string>();
        var response = "The meaning of life is a profound philosophical question. It explores purpose, existence, and what truly matters.";
        
        foreach (var c in response)
        {
            chunks.Enqueue(c.ToString());
        }
        
        return chunks;
    }
}

class Program
{
    static void Main(string[] args)
    {
        var shared = new Dictionary<string, object>
        {
            ["prompt"] = "What's the meaning of life?"
        };
        
        var streamNode = new StreamNode();
        var flow = new Flow<TShared>(streamNode);
        flow.Run(shared).Wait();
    }
}
