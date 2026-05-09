use std::io::Cursor;

use image::codecs::png::PngEncoder;
use image::imageops::{resize, FilterType};
use image::{ColorType, GrayImage, ImageEncoder};

use crate::buffers::ByteBuffer;

pub(crate) fn preprocess_bgra_to_png(
    pixels: *const u8,
    width: u32,
    height: u32,
    stride: u32,
) -> Result<ByteBuffer, String> {
    if pixels.is_null() {
        return Err("Input pixel pointer is null".to_string());
    }

    if width == 0 || height == 0 {
        return Err("Input image width and height must be greater than zero".to_string());
    }

    let min_stride = width
        .checked_mul(4)
        .ok_or_else(|| "Input image stride overflow".to_string())?;
    if stride < min_stride {
        return Err("Input image stride is smaller than width * 4".to_string());
    }

    let source_len = (stride as usize)
        .checked_mul(height as usize)
        .ok_or_else(|| "Input image buffer length overflow".to_string())?;
    let source = unsafe { std::slice::from_raw_parts(pixels, source_len) };

    let mut luma = bgra_to_luma(source, width, height, stride, min_stride);
    stretch_contrast(&mut luma);
    sharpen_luma(&mut luma, width, height);

    let image = GrayImage::from_raw(width, height, luma)
        .ok_or_else(|| "Failed to build grayscale OCR image".to_string())?;
    let processed = scale_for_ocr(image);
    let png = encode_png_luma(&processed)?;

    Ok(ByteBuffer::from_vec(png))
}

fn bgra_to_luma(source: &[u8], width: u32, height: u32, stride: u32, min_stride: u32) -> Vec<u8> {
    let mut luma = Vec::with_capacity((width as usize) * (height as usize));

    for y in 0..height as usize {
        let row_start = y * stride as usize;
        let row = &source[row_start..row_start + min_stride as usize];

        for pixel in row.chunks_exact(4) {
            let b = pixel[0] as u32;
            let g = pixel[1] as u32;
            let r = pixel[2] as u32;
            let a = pixel[3] as u32;

            let value = ((r * 77 + g * 150 + b * 29) >> 8) as u8;
            let composited = if a < 255 {
                ((value as u32 * a + 255 * (255 - a)) / 255) as u8
            } else {
                value
            };
            luma.push(composited);
        }
    }

    luma
}

fn percentile_from_histogram(histogram: &[u32; 256], total: u32, percentile: f32) -> u8 {
    if total == 0 {
        return 0;
    }

    let threshold = ((total as f32 - 1.0) * percentile).round() as u32;
    let mut cumulative = 0;

    for (value, count) in histogram.iter().enumerate() {
        cumulative += count;
        if cumulative > threshold {
            return value as u8;
        }
    }

    255
}

fn stretch_contrast(buffer: &mut [u8]) {
    let mut histogram = [0_u32; 256];
    for value in buffer.iter() {
        histogram[*value as usize] += 1;
    }

    let total = buffer.len() as u32;
    let low = percentile_from_histogram(&histogram, total, 0.01);
    let high = percentile_from_histogram(&histogram, total, 0.99);

    if high <= low.saturating_add(8) {
        return;
    }

    let range = (high - low) as f32;
    for value in buffer.iter_mut() {
        let normalized = ((*value).saturating_sub(low) as f32 / range * 255.0).round();
        *value = normalized.clamp(0.0, 255.0) as u8;
    }
}

fn sharpen_luma(buffer: &mut [u8], width: u32, height: u32) {
    if width < 3 || height < 3 {
        return;
    }

    let source = buffer.to_vec();
    let width = width as usize;
    let height = height as usize;

    for y in 1..height - 1 {
        for x in 1..width - 1 {
            let index = y * width + x;
            let mut sum = 0_u32;

            for offset_y in y - 1..=y + 1 {
                for offset_x in x - 1..=x + 1 {
                    sum += source[offset_y * width + offset_x] as u32;
                }
            }

            let blurred = sum as f32 / 9.0;
            let original = source[index] as f32;
            let sharpened = original + (original - blurred) * 0.65;
            buffer[index] = sharpened.round().clamp(0.0, 255.0) as u8;
        }
    }
}

fn scale_for_ocr(image: GrayImage) -> GrayImage {
    let max_side = image.width().max(image.height());
    let scale = if max_side < 900 {
        3
    } else if max_side < 1600 {
        2
    } else {
        1
    };

    if scale > 1 {
        resize(
            &image,
            image.width().saturating_mul(scale),
            image.height().saturating_mul(scale),
            FilterType::CatmullRom,
        )
    } else {
        image
    }
}

fn encode_png_luma(image: &GrayImage) -> Result<Vec<u8>, String> {
    let mut png = Vec::new();
    let encoder = PngEncoder::new(Cursor::new(&mut png));
    encoder
        .write_image(
            image.as_raw(),
            image.width(),
            image.height(),
            ColorType::L8.into(),
        )
        .map_err(|err| err.to_string())?;

    Ok(png)
}
