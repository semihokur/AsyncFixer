AsyncFixer helps developers in finding and correcting common `async/await` *misuses* (i.e., anti-patterns). It currently detects 5 common kinds of async/await misuses and fixes 3 of them via program transformations. AsyncFixer has been tested with thousands of open-source C# projects and successfully handles many corner cases. It is also one of the most common analyzers used in C# projects from Microsoft.

AsyncFixer will work just in the IDE and work as an analyzer on every project you open in Visual Studio. It can also operate in batch mode to correct all misuses in the document, project, or solution. You can download the VSIX from [here](https://visualstudiogallery.msdn.microsoft.com/03448836-db42-46b3-a5c7-5fc5d36a8308).

If you want AsyncFixer to deploy as a NuGet package and work as a project-local analyzer that participates in builds, you can use the nuget package. Attaching an analyzer to a project means that the analyzer travels with the project to source control and so it is easy to apply the same rule for the team. You can download the nuget package from [here](https://www.nuget.org/packages/AsyncFixer).

Here are `async/await` *misuses* (i.e., anti-patterns) that AsyncFixer can currently detect:

### AsyncFixer01: Unnecessary async/await usage

There are some async methods where there is no need to use `async/await` keywords. It is important to detect this kind of misuse because adding the async modifier comes at a price. AsyncFixer automatically removes `async/await` keywords from those methods.

![asyncfixer-1.gif](https://raw.githubusercontent.com/semihokur/AsyncFixer/main/img/asyncfixer-1.gif)

### AsyncFixer02: Long-running or blocking operations inside an async method

Developers use some potentially long-running or blocking operations inside async methods even though there are corresponding asynchronous versions of these methods in .NET or third-party libraries. Some examples for such operations: `Task.Wait()`, `Task.Result`, `StreamReader.ReadToEnd()`, `Thread.Sleep()`, etc.

AsyncFixer automatically replaces those operations with their corresponding asynchronous operations and inserts an `await` expression. For instance, it converts `Thread.Sleep(...)` to `await Task.Delay(...)`.

![asyncfixer-2.gif](https://raw.githubusercontent.com/semihokur/AsyncFixer/main/img/asyncfixer-2.gif)

### AsyncFixer03: Fire-and-forget *async-void* methods and delegates

Some async methods and delegates are fire-and-forget, which return `void`. Unless a method is only called as an event handler, it must be awaitable. Otherwise, it is a code smell because it complicates control flow and makes error detection/correction difficult. Unhandled exceptions in those *async-void* methods and delegates will crash the process as well.

AsyncFixer automatically converts `void` to `Task`.

![asyncfixer-3.gif](https://raw.githubusercontent.com/semihokur/AsyncFixer/main/img/asyncfixer-3.gif) 

### AsyncFixer04: Fire-and-forget async call inside an *using* block

Inside a `using` block, developers insert a fire-and-forget async call which uses a disposable object as a parameter or target object. It can cause potential exceptions or wrong results. For instance, developers create a `Stream` in the `using` statement, pass it to the asynchronous method, and then `Stream` will be implicitly disposed via a `using` block. When the asynchronous method comes around to writing to `Stream`, it is (very likely) already disposed and you will have an exception.

### AsyncFixer05: Downcasting from a nested task to an outer task.

Downcasting from a nested task to a task or awaiting a nested task is dangerous. There is no way to wait for and get the result of the child task. This usually occurs when mixing `async/await` keywords with the old threading APIs such as `TaskFactory.StartNew`. Here is an example: 

```
async Task foo()
{
    Console.WriteLine("Hello");
    await Task.Factory.StartNew(() => Task.Delay(1000)); // StartNew call returns a nested task: Task<Task>
    Console.WriteLine("World");
}
```
A developer might expect one-second latency between "Hello" and "World" lines. However, those strings will be printed instantaneously without any latency. The reason is that we await a nested task, which is the return type of `StartNew` call. When we await the nested task, the return value is the inner task that is the result of `Task.Delay` call. As we do not await the inner task, we do not see the effect of the delay call. There are three possible fixes: 

1. We can await the inner task as well: 

```
await (await Task.Factory.StartNew(() => Task.Delay(1000)));
```

2. We can use `Unwrap` to expose the inner task to the `await` expression:

```
await Task.Factory.StartNew(() => Task.Delay(1000)).Unwrap();
```

3. If you do not have reasons to use `TaskFactory.StartNew` such as `TaskCreationOptions` and a custom `TaskScheduler`, we should always use `Task.Run` to automatically unwrap the inner task.

```
await Task.Run(() => Task.Delay(1000));
```
