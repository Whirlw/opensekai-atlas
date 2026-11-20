// Alpha selects the optional palette. The source texture still supplies its outline and highlights.
fixed4 _NextRushPalette;
fixed3 nextRushColor(fixed3 source)
{
    fixed light = dot(source, fixed3(0.299, 0.587, 0.114));
    fixed3 styled = lerp(0, _NextRushPalette.rgb, saturate(light * 2));
    styled = lerp(styled, 1, saturate(light * 2 - 1));
    return lerp(source, styled, _NextRushPalette.a);
}
