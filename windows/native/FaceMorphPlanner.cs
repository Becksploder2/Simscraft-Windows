using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Simcraft.Cas;

public sealed record MorphStep(string Feature,int Dx,int Dy,double Error);
/// <summary>Converts normalized MediaPipe region deltas into bounded CAS movements.
/// Movement directions and sensitivities are user-calibrated, not assumed to match EA's hidden sliders.</summary>
public static class FaceMorphPlanner {
    public static IReadOnlyList<MorphStep> Plan(string analysisJson,
        IReadOnlyDictionary<string,(double X,double Y)> sensitivity, int maxPixels=35) {
        using var document=JsonDocument.Parse(analysisJson);
        var root=document.RootElement;
        var reference=FirstFace(root,"reference");
        var cas=FirstFace(root,"cas");
        var output=new List<MorphStep>();
        foreach(var feature in new[]{"eyes","eyebrows","nose","mouth_lips","face_shape"}) {
            if(!sensitivity.TryGetValue(feature,out var scale)) continue;
            double deltaWidth, deltaHeight;
            if(feature=="face_shape") {
                // Composite measurement, but only ONE bounded drag at the calibrated face-shape point.
                // Never reuse old cheek/jaw/forehead drag coordinates or target the neck.
                var regions=new[]{"forehead","cheeks","chin_jaw"};
                double sumW=0,sumH=0;int count=0;
                foreach(var region in regions) {
                    if(!reference.TryGetProperty(region,out var ra)||!cas.TryGetProperty(region,out var rb))continue;
                    sumW+=ra.GetProperty("width").GetDouble()-rb.GetProperty("width").GetDouble();
                    sumH+=ra.GetProperty("height").GetDouble()-rb.GetProperty("height").GetDouble();
                    count++;
                }
                if(count==0)continue;
                deltaWidth=sumW/count;deltaHeight=sumH/count;
            } else {
                if(!reference.TryGetProperty(feature,out var a)||!cas.TryGetProperty(feature,out var b))continue;
                deltaWidth=a.GetProperty("width").GetDouble()-b.GetProperty("width").GetDouble();
                deltaHeight=a.GetProperty("height").GetDouble()-b.GetProperty("height").GetDouble();
            }
            int dx=(int)Math.Round(Math.Clamp(deltaWidth*scale.X,-maxPixels,maxPixels));
            int dy=(int)Math.Round(Math.Clamp(deltaHeight*scale.Y,-maxPixels,maxPixels));
            if(dx!=0||dy!=0)output.Add(new MorphStep(feature,dx,dy,Math.Sqrt(deltaWidth*deltaWidth+deltaHeight*deltaHeight)));
        }
        return output.OrderByDescending(x=>x.Error).ToList();
    }
    static JsonElement FirstFace(JsonElement root,string key) {
        if(!root.TryGetProperty(key,out var item)||item.GetProperty("face_count").GetInt32()==0)
            throw new InvalidDataException("Both reference and CAS faces must be detected.");
        return item.GetProperty("faces")[0].GetProperty("regions");
    }
}
