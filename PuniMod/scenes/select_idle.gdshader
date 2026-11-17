shader_type canvas_item;
render_mode unshaded;

uniform float idle_phase : hint_range(0.0, 1.0) = 0.0;
uniform float cloud_phase : hint_range(0.0, 1.0) = 0.0;

const vec2 ART_SIZE = vec2(1920.0, 1080.0);
const vec2 SEAL_CENTERS[7] = vec2[7](
	vec2(1332, 71), vec2(1596, 200), vec2(1688, 422), vec2(1653, 616),
	vec2(948, 608), vec2(912, 398), vec2(1017, 187)
);

float ellipse_falloff(vec2 p, vec2 center, vec2 radii) {
	return 1.0 - smoothstep(0.82, 1.18, length((p - center) / radii));
}

float clear_of_glyphs(vec2 p) {
	float closest = 1000.0;
	for (int i = 0; i < 7; i++) {
		closest = min(closest, length(p - SEAL_CENTERS[i]));
	}
	return smoothstep(35.0, 53.0, closest);
}

// Leave a margin around the foreground, including the fine blue ribbons and
// the gold ring. Neither the source nor the destination of a cloud sample
// may cross into the foreground.
float cloud_openness(vec2 p) {
	float foreground = ellipse_falloff(p, vec2(1353, 279), vec2(374, 248));
	foreground = max(foreground, ellipse_falloff(p, vec2(1351, 546), vec2(400, 177)));
	foreground = max(foreground, ellipse_falloff(p, vec2(1115, 747), vec2(232, 203)));
	foreground = max(foreground, ellipse_falloff(p, vec2(1495, 691), vec2(190, 164)));
	foreground = max(foreground, ellipse_falloff(p, vec2(1004, 835), vec2(144, 189)));
	foreground = max(foreground, ellipse_falloff(p, vec2(1574, 764), vec2(88, 115)));
	foreground = max(foreground, ellipse_falloff(p, vec2(1374, 632), vec2(94, 224)));
	foreground = max(foreground, ellipse_falloff(p, vec2(803, 533), vec2(283, 151)));
	foreground = max(foreground, ellipse_falloff(p, vec2(1690, 490), vec2(136, 202)));
	foreground = max(foreground, ellipse_falloff(p, vec2(1750, 300), vec2(128, 77)));
	foreground = max(foreground, ellipse_falloff(p, vec2(1691, 216), vec2(83, 109)));
	float ring_radius = length((p - vec2(1298, 441)) / vec2(390, 371));
	float ring = 1.0 - smoothstep(0.13, 0.26, abs(ring_radius - 1.0));
	foreground = max(foreground, ring);
	float edge = smoothstep(0.0, 104.0, min(p.x, ART_SIZE.x - p.x));
	float cloud_band = max(smoothstep(270.0, 570.0, p.y), smoothstep(1510.0, 1810.0, p.x));
	return (1.0 - foreground) * edge * cloud_band;
}

vec3 cloud_sample(sampler2D art, vec2 p, vec3 still_color, float phase, float openness) {
	// Increase the offset at a constant rate. This sample is invisible when
	// it wraps, with the other copy already moving in the same direction.
	float age = fract(phase);
	vec2 source = p - vec2((age - 0.32) * 360.0, 0.0);
	float blend = openness * cloud_openness(source);
	return mix(still_color, texture(art, source / ART_SIZE).rgb, blend);
}

vec3 drifting_clouds(sampler2D art, vec2 p, vec3 still_color, float phase) {
	float openness = cloud_openness(p);
	if (openness <= 0.001) {
		return still_color;
	}
	float first_opacity = smoothstep(0.05, 0.16, phase) * (1.0 - smoothstep(0.55, 0.66, phase));
	vec3 first = cloud_sample(art, p, still_color, phase, openness);
	vec3 second = cloud_sample(art, p, still_color, phase + 0.5, openness);
	return mix(second, first, first_opacity);
}

vec2 closest_on_segment(vec2 p, vec2 a, vec2 b) {
	vec2 ab = b - a;
	return a + ab * clamp(dot(p - a, ab) / dot(ab, ab), 0.0, 1.0);
}

// The three sections describe a curved strand. Only its free length bends,
// with the tip moving farther than the section nearest the body.
float strand_weight(vec2 p, vec2 tip, vec2 bend1, vec2 bend2, vec2 anchor, float width) {
	vec2 near_a = closest_on_segment(p, tip, bend1);
	vec2 near_b = closest_on_segment(p, bend1, bend2);
	vec2 near_c = closest_on_segment(p, bend2, anchor);
	float distance_to_strand = min(length(p - near_a), min(length(p - near_b), length(p - near_c)));
	float feather = 1.0 - smoothstep(width * 0.47, width, distance_to_strand);
	float free_length = smoothstep(12.0, length(tip - anchor) * 0.77, length(p - anchor));
	return feather * free_length;
}

vec2 tentacle_offset(vec2 p, float phase) {
	if (p.x < 925.0 || p.x > 1753.0 || p.y < 17.0 || p.y > 433.0) {
		return vec2(0.0);
	}
	// These tracks follow five gold tendrils in the original illustration.
	float high_left = strand_weight(p, vec2(1134, 51), vec2(1134, 167), vec2(1261, 224), vec2(1271, 295), 59.0);
	float mid_left = strand_weight(p, vec2(1034, 230), vec2(1083, 319), vec2(1195, 324), vec2(1301, 354), 50.0);
	float low_left = strand_weight(p, vec2(986, 351), vec2(1073, 381), vec2(1163, 363), vec2(1292, 391), 43.0);
	float inner_right = strand_weight(p, vec2(1427, 215), vec2(1491, 253), vec2(1512, 302), vec2(1458, 366), 42.0);
	float outer_right = strand_weight(p, vec2(1710, 167), vec2(1655, 263), vec2(1559, 299), vec2(1529, 382), 51.0);

	vec2 offset = high_left * vec2(14.0 * sin(phase + 0.35), 7.0 * sin(phase + 1.5));
	offset += mid_left * vec2(7.5 * sin(phase - 0.8), 12.0 * sin(phase - 0.2));
	offset += low_left * vec2(8.0 * sin(phase - 1.75), 9.0 * sin(phase - 1.1));
	offset += inner_right * vec2(9.0 * sin(phase + 1.85), 8.0 * sin(phase + 2.8));
	offset += outer_right * vec2(11.0 * sin(phase + 0.9), 13.0 * sin(phase + 1.8));
	// The helix and torso stay pinned even where the strands overlap.
	float core = ellipse_falloff(p, vec2(1374, 338), vec2(68, 204));
	return offset * (1.0 - core) * (1.0 - smoothstep(399.0, 433.0, p.y));
}

float seal_glow(vec2 p, float phase) {
	float glow = 0.0;
	for (int i = 0; i < 7; i++) {
		float local = 1.0 - smoothstep(13.0, 35.0, length(p - SEAL_CENTERS[i]));
		float pulse = 0.5 + 0.5 * sin(phase - float(i) * 0.85);
		glow += local * pulse * pulse;
	}
	return glow;
}

void fragment() {
	float screen_aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
	float art_aspect = ART_SIZE.x / ART_SIZE.y;
	vec2 cover = min(vec2(1.0), vec2(screen_aspect / art_aspect, art_aspect / screen_aspect));
	vec2 uv = (SCREEN_UV - 0.5) * cover + 0.5;
	vec2 p = uv * ART_SIZE;
	float phase = idle_phase * TAU;
	vec4 base = texture(TEXTURE, uv);
	vec3 art = drifting_clouds(TEXTURE, p, base.rgb, fract(cloud_phase + 0.22));

	vec2 offset = tentacle_offset(p, phase) * clear_of_glyphs(p);
	if (dot(offset, offset) > 0.0001) {
		art += texture(TEXTURE, (p - offset) / ART_SIZE).rgb - base.rgb;
	}

	float warm_glyph = smoothstep(0.08, 0.36, base.r - base.b);
	art += vec3(1.0, 0.78, 0.48) * seal_glow(p, phase) * (0.012 + 0.06 * warm_glyph);
	COLOR = vec4(art, base.a);
}
