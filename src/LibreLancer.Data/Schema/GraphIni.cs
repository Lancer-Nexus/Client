// MIT License - Copyright (c) Callum McGing
// This file is subject to the terms and conditions defined in
// LICENSE, which is part of this source code package

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LibreLancer.Data.Ini;
using LibreLancer.Data.IO;

namespace LibreLancer.Data.Schema;

public class GraphIni
{
    public List<FloatGraph> FloatGraphs = [];
    public List<ColorGraph> ColorGraphs = [];

    public FloatGraph? FindFloatGraph(string nickname)
    {
        var result = FloatGraphs.Where(s => string.Equals(s.Name, nickname, StringComparison.InvariantCultureIgnoreCase)).ToArray();
        return result.Count() == 1 ? result.First() : null;
    }

    public ColorGraph? FindColorGraph(string nickname)
    {
        var result = ColorGraphs.Where(s => string.Equals(s.Name, nickname, StringComparison.InvariantCultureIgnoreCase)).ToArray();
        return result.Length == 1 ? result[0] : null;
    }

    public void AddGraphIni(string path, FileSystem vfs, IniStringPool? stringPool = null)
    {
        foreach (var section in IniFile.ParseFile(path, vfs, false, stringPool))
        {
            if (section.Name.ToLowerInvariant() != "igraph")
                throw new Exception("Unexpected section in Graph ini: " + section.Name);
            string? nickname = null;
            FloatGraph? fg = null;
            ColorGraph? cg = null;
            bool skip = false;
            foreach (var e in section)
            {
                if (skip)
                    break;
                switch (e.Name.ToLowerInvariant())
                {
                    case "nickname":
                        nickname = e[0].ToString();
                        break;
                    case "type":
                        var t = e[0].ToString().ToUpperInvariant();
                        if (t == "FLOAT")
                            fg = new FloatGraph();
                        else if (t == "COLOR")
                            cg = new ColorGraph();
                        else
                            skip = true;
                        break;
                    case "point":
                        if (fg == null && cg == null)
                            throw new Exception("Point appearing after type");
                        if (fg != null)
                            fg.Points.Add(new Vector2(e[0].ToInt32(), e[1].ToSingle()));
                        else
                            cg!.Points.Add(new ColorGraphPoint(e[0].ToSingle(), unchecked((uint)e[1].ToInt32())));
                        break;
                }
            }
            if (skip)
                continue;

            if (fg != null)
            {
                fg.Name = nickname!;
                FloatGraphs.Add(fg);
            }
            else if (cg != null)
            {
                cg.Name = nickname!;
                ColorGraphs.Add(cg);
            }
        }
    }
}
