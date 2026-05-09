use image::RgbaImage;
use xcap::Monitor;

use crate::buffers::{ScreenshotBuffer, VirtualScreenBounds};

pub(crate) fn capture_region(
    x: i32,
    y: i32,
    width: u32,
    height: u32,
) -> Result<ScreenshotBuffer, String> {
    if width == 0 || height == 0 {
        return Err("Capture region width and height must be greater than zero".to_string());
    }

    let monitors = Monitor::all().map_err(|err| err.to_string())?;
    let request_left = x as i64;
    let request_top = y as i64;
    let request_right = request_left + width as i64;
    let request_bottom = request_top + height as i64;

    let mut output = RgbaImage::new(width, height);
    let mut captured_any = false;

    for monitor in monitors {
        let monitor_x = monitor.x().map_err(|err| err.to_string())? as i64;
        let monitor_y = monitor.y().map_err(|err| err.to_string())? as i64;
        let monitor_width = monitor.width().map_err(|err| err.to_string())? as i64;
        let monitor_height = monitor.height().map_err(|err| err.to_string())? as i64;
        let monitor_right = monitor_x + monitor_width;
        let monitor_bottom = monitor_y + monitor_height;

        let left = request_left.max(monitor_x);
        let top = request_top.max(monitor_y);
        let right = request_right.min(monitor_right);
        let bottom = request_bottom.min(monitor_bottom);

        if left >= right || top >= bottom {
            continue;
        }

        let region_x = (left - monitor_x) as u32;
        let region_y = (top - monitor_y) as u32;
        let region_width = (right - left) as u32;
        let region_height = (bottom - top) as u32;
        let image = monitor
            .capture_region(region_x, region_y, region_width, region_height)
            .map_err(|err| err.to_string())?;

        let src = image.as_raw();
        let dst = output.as_mut();
        let src_stride = region_width as usize * 4;
        let dst_stride = width as usize * 4;
        let dst_x = (left - request_left) as usize;
        let dst_y = (top - request_top) as usize;

        for row in 0..region_height as usize {
            let src_start = row * src_stride;
            let dst_start = (dst_y + row) * dst_stride + dst_x * 4;
            dst[dst_start..dst_start + src_stride]
                .copy_from_slice(&src[src_start..src_start + src_stride]);
        }

        captured_any = true;
    }

    if !captured_any {
        return Err(format!(
            "Capture region ({x}, {y}, {width}, {height}) does not intersect any monitor"
        ));
    }

    let mut bytes = output.into_raw();
    for pixel in bytes.chunks_exact_mut(4) {
        pixel.swap(0, 2);
    }

    Ok(ScreenshotBuffer::from_vec(bytes, width, height, width * 4))
}

pub(crate) fn virtual_screen_bounds() -> Result<VirtualScreenBounds, String> {
    let monitors = Monitor::all().map_err(|err| err.to_string())?;
    let mut min_x = i64::MAX;
    let mut min_y = i64::MAX;
    let mut max_x = i64::MIN;
    let mut max_y = i64::MIN;

    for monitor in monitors {
        let x = monitor.x().map_err(|err| err.to_string())? as i64;
        let y = monitor.y().map_err(|err| err.to_string())? as i64;
        let width = monitor.width().map_err(|err| err.to_string())? as i64;
        let height = monitor.height().map_err(|err| err.to_string())? as i64;

        min_x = min_x.min(x);
        min_y = min_y.min(y);
        max_x = max_x.max(x + width);
        max_y = max_y.max(y + height);
    }

    if min_x == i64::MAX {
        return Err("No monitors found".to_string());
    }

    Ok(VirtualScreenBounds::new(
        min_x as i32,
        min_y as i32,
        (max_x - min_x) as u32,
        (max_y - min_y) as u32,
    ))
}
