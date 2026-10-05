class serverlist : serverlist_Designer
{
	serverlist()
	{
		base();
		var e = this.Elements;
		e.mainmenu.OnClick(() => this.Leave());
		this.Widget.OnEscape(() => this.Leave());
		e.listtable.OnDoubleClick(() => this.TryConnect());
		e.refreshlist.OnClick(() => Game.RefreshServers());
		e.connect.OnClick(() => this.TryConnect());
		e.directip.OnClick(() => OpenModal(new ipentry(this)));
		if (Game.GatewayEnabled()) {
			e.listtable.Visible = false;
			e.refreshlist.Visible = false;
			e.connect.Visible = false;
			e.directip.Visible = false;
			e.descriptiontext.Visible = false;
		}
		PlaySound('ui_motion_swish')
		e.animgroupA.Animate('flyinleft', 0, 0.8)
		e.animgroupB.Animate('flyinright', 0, 0.8)
		this.InitNetwork();
		if (Game.GatewayEnabled()) Timer(0.01, () => {
			if (Game.HasDebugLoginCredentials()) this.DebugLogin();
			else this.Login();
		});
	}

	DebugLogin()
	{
		this.Widget.Visible = false;
		this.connecting = new connecting();
		OpenModal(this.connecting);
		Game.LoginWithDebugCredentials();
	}

    Leave()
    {
        this.ExitAnimation(() => {
            Game.StopNetworking();
            if (Game.GatewayEnabled()) Game.Exit();
            else OpenScene("mainmenu");
        });
    }
	TryConnect()
	{
		this.connecting = new connecting();
		OpenModal(this.connecting);
		Game.ConnectSelection();
	}

	InitNetwork()
	{
		Game.StartNetworking();
		if (!Game.GatewayEnabled()) this.Elements.listtable.SetData(Game.ServerList());
	}

	ExitAnimation(f)
	{
		local e = this.Elements
		PlaySound('ui_motion_swish')
		e.animgroupA.Animate('flyoutleft', 0, 0.8)
		e.animgroupB.Animate('flyoutright', 0, 0.8)
		Timer(0.8, f)
	}

	CharacterList()
	{
		if(this.connecting != nil) {
			this.connecting.Close();
		}
		this.ExitAnimation(() => {
			OpenScene("characterlist");
			if (Game.SelectDebugCharacter()) Game.LoadCharacter();
		});
	}

	Update()
	{
		if (Game.GatewayEnabled()) return;
		local scn = this.Elements
		local sv = Game.ServerList()
		scn.connect.Enabled = sv.ValidSelection()
		scn.descriptiontext.Text = sv.CurrentDescription()
	}

	Login()
	{
		if(this.connecting != nil) {
			this.connecting.Close();
			this.connecting = nil;
		}
		if (Game.GatewayEnabled()) this.Widget.Visible = false;
		OpenModal(new login(this));
	}

	IncorrectPassword()
	{
		if(this.connecting != nil) {
			this.connecting.Close();
			this.connecting = nil;
		}
		OpenModal(new login(this, true));
	}

 	Disconnect(reason)
    {
		if(this.connecting != nil) {
			this.connecting.Close();
			this.connecting = nil;
		}
		var id = (reason == "Banned" ? STRID_BANNED : STRID_DISCONNECT);
        OpenModal(new popup(0, id, "ok", () => {
            this.InitNetwork();
            if (Game.GatewayEnabled()) this.Login();
        }));
    }

	UpdateRequired(messageKey)
	{
		if(this.connecting != nil) {
			this.connecting.Close();
			this.connecting = nil;
		}
		local prompt = new popup(0, STRID_DISCONNECT, "ok", () => Game.ExitForUpdate());
		prompt.Elements.contents.SetString(messageKey == "client_protocol_unsupported"
			? "This client protocol is no longer supported. An update is required."
			: "A client update is required before login.");
		OpenModal(prompt);
	}

	RepairRequired()
	{
		if(this.connecting != nil) {
			this.connecting.Close();
			this.connecting = nil;
		}
		local prompt = new popup(0, STRID_DISCONNECT, "ok", () => Game.ExitForRepair());
		prompt.Elements.contents.SetString("Client installation data is missing or damaged. The updater will repair it.");
		OpenModal(prompt);
	}
}




