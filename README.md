# SqlDependencyListener
Listen to sql database changes for a WebAPI controller in .NET

Make sure to add SqlDependency.Start/Stop in the Application start/stop methods in Global.asax. 

Also, when installing SqlDependency and SignalR, there'll be a Startup.cs for the SignalR that will be created.

Usage in an API controller:

    new SqlDependencyController<DbContext.Entity>(en => en.ID == id, ClientMethod, groupName);

    public void ClientMethod(string groupName) {...}

or, if using SignalR,

    public void ClientMethod(string groupName) => 
      GlobalHost.ConnectionManager.GetHubContext<MyHub>().Clients.Group(groupName).SomeClientMethod();
