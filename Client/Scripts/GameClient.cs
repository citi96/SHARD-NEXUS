using Godot;
using System;

namespace Client.Scripts;

public partial class GameClient : Node
{
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        GD.Print("GameClient avviato con successo su Godot + C# 13!");
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }
}
