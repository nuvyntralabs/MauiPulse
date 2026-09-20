using Plugin.Maui.Pulse.Cli;

return await CliHost.RunAsync(args, Console.Out, Console.Error, Console.In).ConfigureAwait(false);
