shader_type canvas_item;
render_mode world_vertex_coords;

uniform float flow_speed = 1.1;
uniform float swell_scale = 0.020;
uniform float ripple_scale = 0.042;
uniform float cross_scale = 0.030;
uniform float shimmer_strength = 0.10;
uniform float highlight_strength = 0.075;
uniform vec4 tint : source_color = vec4(0.88, 0.98, 1.10, 1.0);
uniform vec4 highlight_tint : source_color = vec4(0.70, 0.90, 1.12, 1.0);

varying vec2 moat_pos;

void vertex() {
    moat_pos = VERTEX;
}

void fragment() {
    vec2 uv = UV;
    vec4 base = texture(TEXTURE, uv);
    vec2 river_axis = normalize(vec2(1.0, 0.5));
    vec2 cross_axis = vec2(-river_axis.y, river_axis.x);
    float along = dot(moat_pos, river_axis);
    float across = dot(moat_pos, cross_axis);

    float swell = sin(along * swell_scale - TIME * flow_speed + sin(across * cross_scale + TIME * 0.45) * 0.7);
    float ripple = sin(along * ripple_scale - TIME * (flow_speed * 1.45) + cos(across * (cross_scale * 1.7) - TIME * 0.35) * 0.45);
    float cross_wave = sin(across * (cross_scale * 1.2) + TIME * 0.3);

    float shimmer = 1.0 + swell * shimmer_strength + ripple * (shimmer_strength * 0.45) + cross_wave * 0.025;
    float highlight_wave = 0.5 + 0.5 * sin(along * (swell_scale * 1.35) - TIME * (flow_speed * 1.15) + cross_wave * 0.8);
    float highlight_mask = smoothstep(0.72, 0.95, highlight_wave) * highlight_strength;

    vec3 lit = base.rgb * tint.rgb * shimmer;
    lit += highlight_tint.rgb * highlight_mask;

    COLOR = vec4(lit, base.a * tint.a);
}
