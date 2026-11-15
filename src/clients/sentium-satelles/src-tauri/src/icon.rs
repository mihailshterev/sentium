//! Draws the tray icon at run time: a ring coloured by connection state, with a red dot while anything is unread.

use tauri::image::Image;

use crate::agent::Status;

const SIZE: u32 = 32;

type Rgb = (u8, u8, u8);

const ACCENT: Rgb = (0x7C, 0x9C, 0xFF);
const MUTED: Rgb = (0x8A, 0x90, 0xA0);
const WARNING: Rgb = (0xF5, 0xB8, 0x4B);
const DANGER: Rgb = (0xFF, 0x5C, 0x6C);

pub fn render(status: Status, has_unread: bool) -> Image<'static> {
    let color = match status {
        Status::Connected => ACCENT,
        Status::Untrusted => DANGER,
        Status::Offline | Status::Connecting => WARNING,
        Status::NotPaired => MUTED,
    };

    let mut rgba = vec![0u8; (SIZE * SIZE * 4) as usize];
    let center = SIZE as f32 / 2.0;

    for y in 0..SIZE {
        for x in 0..SIZE {
            let (px, py) = (x as f32 + 0.5, y as f32 + 0.5);
            let distance = ((px - center).powi(2) + (py - center).powi(2)).sqrt();

            let ring = coverage(distance, 11.0, 14.0);
            let core = coverage(distance, -1.0, 5.0);
            let mut pixel = blend(None, color, ring.max(core));

            if has_unread {
                let badge = ((px - 25.0).powi(2) + (py - 7.0).powi(2)).sqrt();
                pixel = blend(pixel, DANGER, coverage(badge, -1.0, 6.5));
            }

            if let Some((r, g, b, a)) = pixel {
                let i = ((y * SIZE + x) * 4) as usize;
                rgba[i..i + 4].copy_from_slice(&[r, g, b, a]);
            }
        }
    }

    Image::new_owned(rgba, SIZE, SIZE)
}

/// How much of a pixel at `distance` from the centre lies within the band [inner, outer], antialiased over one pixel.
fn coverage(distance: f32, inner: f32, outer: f32) -> f32 {
    ((distance - inner + 0.5).clamp(0.0, 1.0)).min((outer - distance + 0.5).clamp(0.0, 1.0))
}

fn blend(under: Option<(u8, u8, u8, u8)>, color: Rgb, alpha: f32) -> Option<(u8, u8, u8, u8)> {
    if alpha <= 0.0 {
        return under;
    }

    let a = (alpha * 255.0).round() as u8;
    match under {
        Some((r, g, b, ua)) if alpha < 1.0 => {
            let mix = |over: u8, base: u8| {
                (f32::from(over) * alpha + f32::from(base) * (1.0 - alpha)).round() as u8
            };
            Some((mix(color.0, r), mix(color.1, g), mix(color.2, b), ua.max(a)))
        }
        _ => Some((color.0, color.1, color.2, a)),
    }
}
