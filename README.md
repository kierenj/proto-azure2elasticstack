# proto-azure2elasticstack

This runs on my Windows machine at around 100-200MB RAM usage.  On Linux, it either sites at 800MB (standard Linux VM) or ~2GB (Kubernetes pod).

See: https://stackoverflow.com/questions/51983312/net-core-on-linux-lldb-sos-plugin-diagnosing-memory-issue?noredirect=1#comment90941988_51983312

To use: edit Program.cs with Azure app service credentials.

The app reads all available subscriptions, resource groups, resources and their statistics, and writes to the `logs` subfolder in JSON format.
