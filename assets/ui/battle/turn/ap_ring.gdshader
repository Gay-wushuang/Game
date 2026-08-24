shader_type canvas_item;

uniform float progress : hint_range(0.0, 1.0) = 1.0;
uniform vec4 active_color : source_color = vec4(0.196, 0.784, 0.949, 1.0);
uniform float glow_strength : hint_range(0.0, 1.0) = 0.16;
uniform float pulse : hint_range(0.0, 1.0) = 0.0;
uniform float angle_offset : hint_range(-0.5, 0.5) = 0.0;
uniform bool disabled = false;

void fragment() {
    vec4 mask = texture(TEXTURE, UV);
    vec2 centered = UV - vec2(0.5);
    float angle = atan(centered.x, -centered.y);
    float clockwise = mod(angle / 6.28318530718 + angle_offset + 1.0, 1.0);
    float has_progress = step(0.0001, progress);
    float visible_segment = step(clockwise, progress + 0.0001) * has_progress;
    float brightness = disabled ? 0.28 : 1.0 + glow_strength + pulse * 0.18;
    COLOR = vec4(active_color.rgb * brightness, mask.a * active_color.a * visible_segment);
}
