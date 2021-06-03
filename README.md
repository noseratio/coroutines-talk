# Asynchronous coroutines with C# 8.0 and `IAsyncEnumerable`

This is the source repro for my article originally published on [DEV.TO](https://dev.to/noseratio/asynchronous-coroutines-with-c-8-0-and-iasyncenumerable-2e04).

**Updated:** It was an exciting opportunity and experience to speak on this topic at [.NET Conf 2020](https://www.dotnetconf.net). 

My talk is now [available on YouTube](https://www.youtube.com/watch?v=pE6b2Bs3U9Q), and the slide decks are [available in this repro](Asynchronous%20Coroutines%20with%20C%23.pptx).

## Introduction

**TLDR, [skip to a real life example](#skipTo).**

Coroutines are functions that yield and execute cooperatively, the concept that has been around for many decades. According to [Wikipedia](https://en.wikipedia.org/wiki/Coroutine), *coroutines are very similar to threads. However, coroutines are cooperatively multitasked, whereas threads are typically preemptively multitasked*.

Coroutines are useful for script-like scenarios where the code execution flow can be suspended and resumed after each logical step. Internally, they use some sort of programming language syntax sugar for generating state machines methods. 

In the C# world, they have been popularized by [Unity game development platform](https://docs.unity3d.com/Manual/Coroutines.html), and Unity uses [`IEnumerator`](https://docs.microsoft.com/en-us/dotnet/api/system.collections.ienumerator?view=netcore-3.1)-style methods and `yield return` for that. 

Prior to C# 8, it wasn't possible to combine `await` and `yield return` within the same method, making it difficult to use asynchrony inside coroutines. Now, with the compiler's support for `IAsyncEnumerable` it can be done naturally, and we're going to explore this option here.

The execution environment for the code listed here is a Windows Forms .NET Core 3.1 app, but the same techniques can be used anywhere C# code runs. 

## Pull-based approach to coroutines with `IEnumerable`/`IEnumerator`

This approach has been in use for over a decade, since `yield` was introduced in C# 2.0. Here is how a fade effect can be implemented as an `IEnumerator`-method in a Unity video game (borrowed from their [docs](https://docs.unity3d.com/Manual/Coroutines.html)). The use of `yield return` allows to "spread" the `for` loop across multiple frame generation iterations:

```C#
IEnumerator Fade() 
{
    for (float ft = 1f; ft >= 0; ft -= 0.1f) 
    {
        Color c = renderer.material.color;
        c.a = ft;
        renderer.material.color = c;
        yield return null;
    }
}
```

Traditionally, we use `yield return` for synchronously generating sequences of data to be further processed with LINQ operators. 
In contrast, with coroutines it's about code rather than data, and we use `yield return` as a tool to break the code into multiple individually executed chunks. 

This is convenient, because we can use all the normal control flow statements (`if`, `for`, `while`, `foreach`, `using` etc) 
where otherwise we would have to use a chain of callbacks. There's a notable limitation though, 
C# doesn't allow `yield` inside a `try {}` block.

Let's create [our own example](https://github.com/noseratio/coroutines-talk/blob/main/Coroutines/CoroutineDemo.cs). We want `CoroutineA` and `CoroutineB` to execute cooperatively on the primary UI thread. In real life, they might be drawing animation effects or doing background spellchecking, syntax highlighting or other specific `ViewModel`/UI updates.

Here, to keep it simple, we'll just be using the console for some visual progress output:  

```C#
private static IEnumerable<int> CoroutineA()
{
    for (int i = 0; i < 80; i++)
    {
        Console.SetCursorPosition(0, 0);
        Console.Write($"{nameof(CoroutineA)}: {new String('A', i)}");
        yield return i;
    }
}

private static IEnumerable<int> CoroutineB()
{
    for (int i = 0; i < 80; i++)
    {
        Console.SetCursorPosition(0, 1);
        Console.Write($"{nameof(CoroutineB)}: {new String('B', i)}");
        yield return i;
    }
}
```

The execution flow can be illustrated by this diagram:

![Coroutines flow](https://github.com/noseratio/coroutines-talk/raw/main/coroutines-flow.png)

To run these two coroutines cooperatively, we need a *dispatcher*, the code also known as a coroutine driver. Its purpose is to advance the execution flow of each coroutine to the next step, from one `yield return` to another. That can be done upon timer intervals, user input events, or even something like `IObservable`-subscriptions in ReactiveX workflows.  

We will be using a Windows Forms timer for this simple example. The dispatcher proactively "pulls" the execution of continuations by calling `IEnumerator.MoveNext` upon each `Tick` event. [`CoroutineCombinator`](https://github.com/noseratio/coroutines-talk/blob/c5d917a54a40e9059af69e23f51171e16e0d8469/Coroutines/CoroutineCombinator.cs#L13) is a helper to combine two `IEnumerable` streams into one. 

Here's the [dispatcher code](https://github.com/noseratio/coroutines-talk/blob/c5d917a54a40e9059af69e23f51171e16e0d8469/Coroutines/CoroutineDemo.cs#L47):

```C#
private static async ValueTask RunCoroutinesAsync(CancellationToken token)
{
    // combine two IEnumerable sequences into one and get an IEnumerator for it
    using var combined = CoroutineCombinator<int>.Combine(
        CoroutineA, 
        CoroutineB)
        .GetEnumerator();

    var tcs = new TaskCompletionSource<bool>();
    using var rego = token.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: true);

    using var timer = new System.Windows.Forms.Timer { Interval = 50 };
    timer.Tick += (s, e) =>
    {
        try
        {
            // upon each timer tick,
            // pull/execute the next slice 
            // of the combined coroutine code flow
            if (!combined.MoveNext())
            {
                tcs.TrySetResult(true);
            }
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
    };

    timer.Start();
    await tcs.Task;
}
```

Running it:

![Running pull-based coroutines](https://github.com/noseratio/coroutines-talk/raw/main/running-coroutines.png)

Instead of using `IEnumerable`/`yield return`, we could've tried achieving the same with `async`/`await`:

```C#
private static async Task CoroutineA()
{
    for (int i = 0; i < 80; i++)
    {
        Console.SetCursorPosition(0, 0);
        Console.Write($"{nameof(CoroutineA)}: {new String('A', i)}");
        // Task.Yield behavior depends, see https://stackoverflow.com/a/23441833
        await Task.Yield(); 
    }
}
```

However, this would have a somewhat different semantic. We'd only be notified about the completion of the whole method, not the intermediate steps (versus `yield return`). So, we'd lose precise control over how the execution flow gets suspended and resumed at the points of `await`. It's possible to implement a custom `TaskScheduler`, `SynchronizationContext` or a [C# awaitable](https://stackoverflow.com/a/22854116) to control that, but that'd come with added code complexity and runtime overhead. 

Ideally, we should be using `async`/`await` for awaiting the results of an actual asynchronous API, rather than for suspending the execution flow, and using `yield return` for the latter.

Prior to C# 8, it wasn't possible to combine the two within the same method, but now we can do that.

## Push-based approach to coroutines (or async pull) with `IAsyncEnumerable`/`IAsyncIEnumerator`

In 2018, C# 8.0 introduced support for [asynchronous streams](https://docs.microsoft.com/en-us/dotnet/csharp/tutorials/generate-consume-asynchronous-stream) with new language and runtime features like: 
 - [`IAsyncEnumerable`](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.iasyncenumerable-1?view=dotnet-plat-ext-3.1)
 - [`IAsyncEnumerator`](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.iasyncenumerator-1?view=dotnet-plat-ext-3.1) 
 - [`IAsyncDisposable`](https://docs.microsoft.com/en-us/dotnet/api/system.iasyncdisposable?view=dotnet-plat-ext-3.1) 
 - [`await foreach`](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/foreach-in) 
 - [`await using`](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-statement)

If you are not familiar with the concept of asynchronous streams, I'd highly recommend reading ["Iterating with Async Enumerables in C# 8"](https://docs.microsoft.com/en-us/archive/msdn-magazine/2019/november/csharp-iterating-with-async-enumerables-in-csharp-8), by [Stephen Toub](https://devblogs.microsoft.com/dotnet/author/toub/).

In a few words, similar to how `IEnumerable` is used to produce a stream of data to be consumed by pulling with `IEnumerator.MoveNext`, `IAsyncEnumerable` is used to produce a stream of events that can be asynchronously consumed by awaiting the result of `IAsyncEnumerator.MoveNextAsync`. I have a related blog post, ["C# events as asynchronous streams with ReactiveX or Channels"](https://dev.to/noseratio/c-events-as-asynchronous-streams-with-reactivex-or-channels-82k).

And so by analogy with `IEnumerable`, we can use `IAsyncEnumerable`-methods to implement coroutines with async calls inside.

Before we get to a [real life example](###-A-real-life-scenario) of that, let's reproduce what we've done so far with `IEnumerable`-based `CoroutineA` and `CoroutineB`, but [using `IAsyncEnumerable` this time](https://github.com/noseratio/coroutines-talk/blob/main/Coroutines/AsyncCoroutineDemo.cs). We still want to run `yield return` continuations upon fixed timer intervals, but we also want to make sure there is no pending user input in the UI thread's message queue, before we proceed with any micro-task that runs on the UI thread. That's what [`inputIdler.Yield()`](https://github.com/noseratio/coroutines-talk/blob/main/Coroutines/InputIdler.cs) is for below:

```C#
private static async IAsyncEnumerable<int> CoroutineA(
    [EnumeratorCancellation] CancellationToken token)
{
    var inputIdler = new InputIdler();
    for (int i = 0; i < 80; i++)
    {
        // yield to the event loop to process any keyboard/mouse input first
        await inputIdler.Yield(token);
        
        // now we could use Task.Run for this to offload it to ThreadPool,
        // but let's pretend this code must execute on the UI thread 
        Console.SetCursorPosition(0, 0);
        Console.Write($"{nameof(CoroutineA)}: {new String('A', i)}");

        yield return i;
    }
}
```

Let's also slow down `CoroutineB` by introducing an async `Delay`, just because now we can:

```C#
private static async IAsyncEnumerable<int> CoroutineB(
    [EnumeratorCancellation] CancellationToken token)
{
    var inputIdler = new InputIdler();
    for (int i = 0; i < 80; i++)
    {
        // yield to the event loop to process any keyboard/mouse input first
        await inputIdler.Yield(token);

        Console.SetCursorPosition(0, 1);
        Console.Write($"{nameof(CoroutineB)}: {new String('B', i)}");

        // slow down
        await Task.Delay(25, token);
        yield return i;
    }
}
```

Both coroutines now run concurrently (still on the same UI thread). We don't want to put (say) `CoroutineA` on hold only because `CoroutineB` is asynchronously waiting for `Task.Delay`. There's still a way to synchronize, which I'll show in the next example. 

Here's [the dispatcher code](https://github.com/noseratio/coroutines-talk/blob/c5d917a54a40e9059af69e23f51171e16e0d8469/Coroutines/AsyncCoroutineDemo.cs#L67):

```C#
private static async ValueTask RunCoroutinesAsync<T>(
    int intervalMs,
    CancellationToken token,
    params Func<CancellationToken, IAsyncEnumerable<T>>[] coroutines)
{
    var tasks = coroutines.Select(async c => 
    {
        var interval = new Interval();
        await foreach (var item in c(token).WithCancellation(token))
        {
            await interval.Delay(intervalMs, token);
        }
    });

    await Task.WhenAll(tasks); 
}
```

Running it:

![Running push-based coroutines](https://github.com/noseratio/coroutines-talk/raw/main/running-async-coroutines.png)

## Synchronizing the flow of asynchronous coroutines. 

Now, what if `CoroutineA` needs to synchronize upon the progress of `CoroutineB`? Below is [a made-up but simple example](https://github.com/noseratio/coroutines-talk/blob/main/Coroutines/AsyncCoroutineDemoMutual.cs), where `CoroutineA` starts progressing only when `CoroutineB` has already been half-way through its own workflow. At that point, `CoroutineB` awaits for `CoroutineA` to catch up, then they both continue running to the end.

We do that with a help of custom [`AsyncCoroutineProxy`](https://github.com/noseratio/coroutines-talk/blob/main/Coroutines/AsyncCoroutineProxy.cs), a helper class that wraps a .NET [`Channel`](https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/) to serve as an asynchronous queue for progress notifications from `IAsyncEnumerator.MoveNextAsync` of `CoroutineB`. 

A `Channel` is like a pipe, we can push objects into one side of the pipe (with [`Channel.Writer.WriteAsync`](https://docs.microsoft.com/en-us/dotnet/api/system.threading.channels.channelwriter-1.writeasync?view=netcore-3.1)), and fetch them as an asynchronous stream from the other side (with [`Channel.Reader.ReadAllAsync`](https://docs.microsoft.com/en-us/dotnet/api/system.threading.channels.channelreader-1.readallasync?view=netcore-3.1)). 

![Coroutines flow](https://github.com/noseratio/coroutines-talk/raw/main/mutual-coroutines-flow.png)

`CoroutineA`:

```C#
private static async IAsyncEnumerable<int> CoroutineA(
    IAsyncCoroutineProxy<int> coroutineProxy,
    [EnumeratorCancellation] CancellationToken token)
{
    var coroutineB = await coroutineProxy.AsAsyncEnumerable(token);
    var interval = new Interval();

    // await for coroutineB to advance by 40 steps
    await foreach (var stepB in coroutineB)
    {
        if (stepB >= 40) break;
        Console.SetCursorPosition(0, 0);
        // display a throber
        Console.Write($"{nameof(CoroutineA)}: {@"-\|/"[stepB % 4]}"); 
        await interval.Delay(intervalMs, token);
    }

    // now do our own thing
    for (int i = 0; i < 80; i++)
    {
        Console.SetCursorPosition(0, 0);
        Console.Write($"{nameof(CoroutineA)}: {new String('A', i)}"); 

        await interval.Delay(intervalMs, token);
        yield return i;
    }
}
```

`CoroutineB`:

```C#
private static async IAsyncEnumerable<int> CoroutineB(
    IAsyncCoroutineProxy<int> coroutineProxy,
    [EnumeratorCancellation] CancellationToken token)
{
    var coroutineA = await coroutineProxy.AsAsyncEnumerable(token);
    var interval = new Interval();

    for (int i = 0; i < 80; i++)
    {
        Console.SetCursorPosition(0, 1);
        Console.Write($"{nameof(CoroutineB)}: {new String('B', i)}");

        await interval.Delay(intervalMs, token);
        yield return i;

        if (i == 40)
        {
            // await for CoroutineA to catch up
            await foreach (var stepA in coroutineA)
            {
                if (stepA >= 40) break;
                Console.SetCursorPosition(0, 1);
                // display a throber
                Console.Write($"{nameof(CoroutineB)}: {new String('B', i)}{@"-\|/"[stepA % 4]}");
                await interval.Delay(intervalMs, token);
            }
        }
    }
}
```

As the [dispatcher code of `AsyncCoroutineProxy`](https://github.com/noseratio/coroutines-talk/blob/c5d917a54a40e9059af69e23f51171e16e0d8469/Coroutines/AsyncCoroutineProxy.cs#L34) asynchronously iterates through the output of `CoroutineB` (with `await foreach`), it relays the received items by writing them to `Channel.Writer`, and then `CoroutineA` reads them from `Channel.Reader`:

```C#
public async Task RunAsync(Func<CancellationToken, IAsyncEnumerable<T>> coroutine, CancellationToken token)
{
    token.ThrowIfCancellationRequested();
    var channel = Channel.CreateUnbounded<T>();
    var writer = channel.Writer;
    var proxy = channel.Reader.ReadAllAsync(token);
    _proxyTcs.SetResult(proxy); 
    
    try
    {
        await foreach (var item in coroutine(token).WithCancellation(token))
        {
            await writer.WriteAsync(item, token);
        }
        writer.Complete();
    }
    catch (Exception ex)
    {
        writer.Complete(ex);
        throw;
    }
}
```

To [run both coroutines](https://github.com/noseratio/coroutines-talk/blob/c5d917a54a40e9059af69e23f51171e16e0d8469/Coroutines/AsyncCoroutineDemoMutual.cs#L74):

```C#
private static async ValueTask RunCoroutinesAsync(CancellationToken token)
{
    var proxyA = new AsyncCoroutineProxy<int>();
    var proxyB = new AsyncCoroutineProxy<int>();

    // start both coroutines
    await Task.WhenAll(
        proxyA.RunAsync(token => CoroutineA(proxyB, token), token),
        proxyB.RunAsync(token => CoroutineB(proxyA, token), token));
}
```

Running it:

![Running push-based coroutines](https://github.com/noseratio/coroutines-talk/raw/main/running-async-mutual-coroutines.png)

<a name="skipTo"></a>

### A real-life scenario

Using [`CoroutineProxy`](https://github.com/noseratio/coroutines-talk/blob/main/Coroutines/AsyncCoroutineProxy.cs), `CoroutineA` and `CoroutineB` can operate as asynchronous producer/consumer to each other, and they can swap these roles. 

That's actually how I use them for automated UI testing. I've recently put together a Windows desktop app called `DevComrade`, **[a small open-source side project](https://github.com/postprintum/devcomrade) for copy-pasting productivity improvement and more**. It uses [Win32 simulated input API](https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput) to asynchronously feed unformatted text into the currently active window, character by character as though it was typed by a person. 

I needed an automated test to simulate that. Below is what I've come up with (the full source [here](https://github.com/postprintum/devcomrade/blob/main/Tests/KeyboardInputTest.cs)).

There's a foreground thread with a form containing a `TextBox` control, and there's a background thread that iteratively calls `SendInput`. A decade ago I'd probably be using something like `ManualResetEvent` and blocking `WaitOne()` to synchronize these two threads at the key points of the test workflow. Today, I can use asynchronous coroutines for that.

For the [foreground thread](https://github.com/postprintum/devcomrade/blob/54110c48019f7d333334585f4e558b5ba241401d/Tests/KeyboardInputTest.cs#L47):

```C#
private enum ForegroundEvents
{
    Ready,
    TextReceived,
    Finish
}

/// <summary>
/// A foreground test workflow that creates a UI form
/// </summary>
private static async IAsyncEnumerable<(ForegroundEvents, object)> ForegroundCoroutine(
    ICoroutineProxy<(BackgroundEvents, object)> backgroundCoroutineProxy,
    [EnumeratorCancellation] CancellationToken token)
{
    Assert.IsInstanceOfType(SynchronizationContext.Current, typeof(WindowsFormsSynchronizationContext));

    // create a test form with TextBox inside
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);

    using var form = new Form
    {
        Text = nameof(KeyboardInputTest),
        Left = 10,
        Top = 10,
        Width = 640,
        Height = 480,
        ShowInTaskbar = false
    };

    using var formClosedHandlerScope = EventHandlerScope<FormClosedEventHandler>.Create(
        (s, e) => cts.Cancel(),
        handler => form.FormClosed += handler,
        handler => form.FormClosed -= handler);

    // add a textbox 
    var textBox = new TextBox { Dock = DockStyle.Fill, Multiline = true };
    form.Controls.Add(textBox);
    form.Show();

    // show
    form.Activate();
    textBox.Focus();

    // coordinate further execution steps with the background coroutine
    await using var backgroundCoroutine =
        await backgroundCoroutineProxy.AsAsyncEnumerator(cts.Token);

    // notify the background coroutine that we're ready
    yield return (ForegroundEvents.Ready, DBNull.Value);

    // await for the background coroutine to also be ready
    var (foregroundEvent, _) = await backgroundCoroutine.GetNextAsync(cts.Token);
    Assert.IsTrue(foregroundEvent == BackgroundEvents.Ready);

    // await for the background coroutine to have fed some keystrokes
    (foregroundEvent, _) = await backgroundCoroutine.GetNextAsync(cts.Token);
    Assert.IsTrue(foregroundEvent == BackgroundEvents.TextSent);

    // await for idle input
    await InputHelpers.InputYield(delay: INPUT_IDLE_CHECK_INTERVAL, token: cts.Token);

    // notify the background coroutine about the text we've actually received
    var text = textBox.Text.Replace(Environment.NewLine, "\n");
    yield return (ForegroundEvents.TextReceived, text);
}
```

For the [background thread](https://github.com/postprintum/devcomrade/blob/54110c48019f7d333334585f4e558b5ba241401d/Tests/KeyboardInputTest.cs#L106):

```C#
private enum BackgroundEvents
{
    Ready,
    TextSent,
    Finish
}

/// <summary>
/// A background test workflow that sends keystrokes
/// </summary>
private static async IAsyncEnumerable<(BackgroundEvents, object)> BackgroundCoroutine(
    ICoroutineProxy<(ForegroundEvents, object)> foregroundCoroutineProxy,
    [EnumeratorCancellation] CancellationToken token)
{
    Assert.IsTrue(SynchronizationContext.Current is WindowsFormsSynchronizationContext);

    await using var foregroundCoroutine = await foregroundCoroutineProxy.AsAsyncEnumerator(token);

    // notify the foreground coroutine that we're ready
    yield return (BackgroundEvents.Ready, DBNull.Value);

    // await for the foreground coroutine to also be ready
    var (foregroundEvent, _) = await foregroundCoroutine.GetNextAsync(token);
    Assert.IsTrue(foregroundEvent == ForegroundEvents.Ready);

    // feed some text to the foreground window
    using var threadInputScope = AttachedThreadInputScope.Create();
    Assert.IsTrue(threadInputScope.IsAttached);

    using (WaitCursorScope.Create())
    {
        await KeyboardInput.WaitForAllKeysReleasedAsync(token);
        await KeyboardInput.FeedTextAsync(TEXT_TO_FEED, token);
    }

    // notify the foreground coroutine that we've fed some text
    yield return (BackgroundEvents.TextSent, DBNull.Value);

    // await for the foreground coroutine to reply with the text
    object text;
    (foregroundEvent, text) = await foregroundCoroutine.GetNextAsync(token);
    Assert.IsTrue(foregroundEvent == ForegroundEvents.TextReceived);
    Assert.AreEqual(text, TEXT_TO_FEED);
}
```

The [dispatcher code](https://github.com/postprintum/devcomrade/blob/54110c48019f7d333334585f4e558b5ba241401d/Tests/KeyboardInputTest.cs#L141), which runs the test itself:

```C#
[TestMethod]
public async Task Feed_text_to_TextBox_and_verify_it_was_consumed()
{
    using var cts = new CancellationTokenSource(); // TODO: test cancellation

    var foregroundCoroutineProxy = new CoroutineProxy<(ForegroundEvents, object)>();
    var backgroundCoroutineProxy = new CoroutineProxy<(BackgroundEvents, object)>();

    await using var foregroundApartment = new WinFormsApartment();
    await using var backgroundApartment = new WinFormsApartment();

    // start both coroutine, each in its own WinForms thread

    var foregroundTask = foregroundCoroutineProxy.Run(
        foregroundApartment, 
        token => ForegroundCoroutine(backgroundCoroutineProxy, token),
        cts.Token);

    var backgroundTask = backgroundCoroutineProxy.Run(
        backgroundApartment,
        token => BackgroundCoroutine(foregroundCoroutineProxy, token),
        cts.Token);

    await Task.WhenAll(foregroundTask, backgroundTask).WithAggregatedExceptions();
}
```
The twisted part about this is that we create `foregroundCoroutineProxy` (for `ForegroundCoroutine`) to 
be passed to `BackgroundCoroutine`, and `foregroundCoroutineProxy` (for `BackgroundCoroutine`) to be passed to `ForegroundCoroutine`. 

So it looks a bit like mutual asynchronous recursion, besides it isn't. The actual backpressure is created by [`CoroutineProxy.RunAsync`](https://github.com/noseratio/coroutines-talk/blob/c9465c5608e515aecedc045dbd440289b6d282f2/Coroutines/AsyncCoroutineProxy.cs#L34), which drives the execution of each coroutine [by `await foreach` loop](https://github.com/noseratio/coroutines-talk/blob/c9465c5608e515aecedc045dbd440289b6d282f2/Coroutines/AsyncCoroutineProxy.cs#L42).  

Note how `BackgroundCoroutine` and `ForegroundCoroutine` use `yield return` and `await GetNextAsync()` 
to synchronize upon each other's state as they are progressing. Everything is asynchronous, there is no blocking calls. Both coroutines are executed on two different threads and actually run in parallel.
In our previous examples, we only dealt with concurrent execution on the same thread. 

### Conclusion

In my opinion, asynchronous coroutines can be an elegant solution to some niche consumer/producer scenarios, especially when there is no clear role separation between producer and consumer. The same kind of problems can certainly be solved with mature and powerful frameworks like [Reactive Extensions](https://github.com/dotnet/reactive) or [Dataflow](https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library). However, the learning curve to use [`IAsyncEnumerable`](https://docs.microsoft.com/en-us/archive/msdn-magazine/2019/november/csharp-iterating-with-async-enumerables-in-csharp-8) and [Channels](https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/) should be really low.

### References

- [*Coroutines* - Wikipedia](https://en.wikipedia.org/wiki/Coroutine)
- [*Coroutines* - Unity](https://docs.unity3d.com/Manual/Coroutines.html)
- [*IResult and Coroutines* - Caliburn.Micro](https://caliburnmicro.com/documentation/coroutines)
- [*Async/await as a replacement of coroutines* - StackOverflow](https://stackoverflow.com/q/22852251/1768303)
- [*Tutorial: Generate and consume async streams using C# 8.0 and .NET Core 3.0*](https://docs.microsoft.com/en-us/dotnet/csharp/tutorials/generate-consume-asynchronous-stream)
- [*Iterating with Async Enumerables in C# 8*](https://docs.microsoft.com/en-us/archive/msdn-magazine/2019/november/csharp-iterating-with-async-enumerables-in-csharp-8)
- [*C# events as asynchronous streams with ReactiveX or Channels*](https://dev.to/noseratio/c-events-as-asynchronous-streams-with-reactivex-or-channels-82k)

### PS

One other useful thing I've learnt while working on this article was how to use the new [`IValueTaskSource`](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.sources.ivaluetasksource-1?view=netcore-3.1) interface to implement a source of lightweight [`ValueTask`](https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/) objects. This can help to greatly reduce allocations while awaiting a `ValueTask` on hot asynchronous loops. For some examples, check the source code of [`SimpleValueTaskSource`](https://github.com/noseratio/coroutines-talk/blob/main/Coroutines/SimpleValueTaskSource.cs), 
[`InputIdler`](https://github.com/noseratio/coroutines-talk/blob/main/Coroutines/InputIdler.cs) and [`TimerSource`](https://github.com/noseratio/coroutines-talk/blob/main/Coroutines/TimerSource.cs).
