using System;
using UnityEngine;

namespace jedjoud.VoxelTerrain.Props {
    // STOP POSTING ABOUT AMONG US! I'M TIRED OF SEEING IT! MY FRIENDS ON TIKTOK SEND ME MEMES, ON DISCORD IT'S FUCKING MEMES!
    // I was in a server, right? and ALL OF THE CHANNELS were just among us stuff. I-I showed my champion underwear to my girlfriend and t-the logo
    // I flipped it and I said "hey babe, when the underwear is sus HAHA DING DING DING DING DING DING DING DI DI DING" I fucking looked at a
    // trashcan and said "THAT'S A BIT SUSSY" I looked at my penis I think of an astronauts helmet and I go "PENIS? MORE LIKE PENSUS" AAAAAAAAAAAAAAHGESFG
    public class PropTypeImpostorTextureArrays {
        public Texture2DArray diffuse;
        public Texture2DArray normal;
        public Texture2DArray mask;

        public PropTypeImpostorTextureArrays(PropType type) {
            diffuse = type.impostorDiffuseMaps;
            normal = type.impostorNormalMaps;
            mask = type.impostorMaskMaps;
        }

        public bool IsValid() {
            return diffuse != null && normal != null && mask != null;
        }
    }
}