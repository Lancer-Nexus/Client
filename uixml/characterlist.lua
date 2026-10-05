class characterlist : characterlist_Designer 
{
    characterlist()
    {
        base()
        var e = this.Elements;
        if (Game.GatewayEnabled()) {
            e.mainmenu.Strid = 0;
            e.mainmenu.InfoId = 0;
            e.mainmenu.Text = "QUIT";
        }
        e.listtable.SetData(Game.CharacterList());
        e.listtable.OnDoubleClick(() => Game.LoadCharacter());
        e.newchar.OnClick(() => Game.RequestNewCharacter());
        e.loadchar.OnClick(() => Game.LoadCharacter());
        e.deletechar.OnClick(() => Game.DeleteCharacter());
        e.serverlist.Visible = false;
		this.Widget.OnEscape(() => this.GoBack());
        e.mainmenu.OnClick(() => this.ExitAnimation(() => {
            Game.StopNetworking();
            if (Game.GatewayEnabled()) Game.Exit();
            else OpenScene("mainmenu");
        }));
    }
    
    //TODO: Animate out
    ExitAnimation(f) => f();

	GoBack()
	{
		this.ExitAnimation(() => {
            Game.StopNetworking();
            if (Game.GatewayEnabled()) Game.Exit();
            else OpenScene("serverlist");
        });
	}
    
    Update()
    {
        var scn = this.Elements;
        var cl = Game.CharacterList();
        scn.loadchar.Enabled = cl.ValidSelection();
	    scn.deletechar.Enabled = cl.ValidSelection();
    }
    
    OpenNewCharacter()
    {
        OpenModal(new textentry((r,n,i) => this.CreateCharacter(r,n,i), StringFromID(STRID_NEW_CHARACTER)));
    }
    
    CreateCharacter(result, name, index)
    {
        if(result == 'ok') Game.NewCharacter(name, index, () => {
			OpenModal(new popup(0, STRID_NAME_TAKEN, "ok"));
		});
    }

	SelectCharFailure()
	{
		OpenModal(new popup(0, STRID_ALREADY_LOGGED_IN, "ok"));
	}
    
    Disconnect()
    {
        OpenModal(new popup(0, STRID_DISCONNECT, "ok", () => this.ExitAnimation(() => OpenScene("mainmenu"))));
    }
}

