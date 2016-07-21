# Kts.Remoting
This project stalled when I determined that the differences between the various options where not very significant.

Reasons to use Kts.Remoting over SignalR:
1. The SignalR project is stalled until the release of Asp.net 5 (in spring of 2016).
2. I deal with pull requests quickly, and am anxious for fixes.
3. Injection in Kts.Remoting is not weird; there are no global lifetimes of injectables.
4. Full proxy support. (See the examples below.)
5. Binary serializer support. (See the examples below.)
6. Hubs are not transitive. You can use singleton services for them.

Reasons to use SignalR instead:
1. You need an Asp.net 5 project now.
2. You need generated Javascript proxies.
3. You do fancy work with the Caller and Group contexts.
