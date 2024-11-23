Native AOT means dealing with compiled models.
The ArtifactsPath feature means dealing with `msbuildprojectextensionspath`.
It's tempting to just write raw sql.
But ef's migrations are so, so nice.

Here's how I got started:

```
cd TrunkFlight.Core
dotnet ef dbcontext optimize --msbuildprojectextensionspath ../artifacts/obj/TrunkFlight.Core/
dotnet ef migrations add --msbuildprojectextensionspath ../artifacts/obj/TrunkFlight.Core/ First
```

Prolly have to run both commands for every model change.
