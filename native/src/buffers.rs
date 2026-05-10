#[repr(C)]
pub struct ScreenshotBuffer {
    data: *mut u8,
    len: usize,
    capacity: usize,
    width: u32,
    height: u32,
    stride: u32,
}

impl ScreenshotBuffer {
    pub(crate) fn empty() -> Self {
        Self {
            data: std::ptr::null_mut(),
            len: 0,
            capacity: 0,
            width: 0,
            height: 0,
            stride: 0,
        }
    }

    pub(crate) fn from_vec(mut bytes: Vec<u8>, width: u32, height: u32, stride: u32) -> Self {
        let buffer = Self {
            data: bytes.as_mut_ptr(),
            len: bytes.len(),
            capacity: bytes.capacity(),
            width,
            height,
            stride,
        };
        std::mem::forget(bytes);
        buffer
    }
}

#[repr(C)]
pub struct VirtualScreenBounds {
    x: i32,
    y: i32,
    width: u32,
    height: u32,
}

impl VirtualScreenBounds {
    pub(crate) fn new(x: i32, y: i32, width: u32, height: u32) -> Self {
        Self {
            x,
            y,
            width,
            height,
        }
    }
}

#[repr(C)]
pub struct ByteBuffer {
    data: *mut u8,
    len: usize,
    capacity: usize,
}

impl ByteBuffer {
    pub(crate) fn empty() -> Self {
        Self {
            data: std::ptr::null_mut(),
            len: 0,
            capacity: 0,
        }
    }

    pub(crate) fn from_vec(mut bytes: Vec<u8>) -> Self {
        let buffer = Self {
            data: bytes.as_mut_ptr(),
            len: bytes.len(),
            capacity: bytes.capacity(),
        };
        std::mem::forget(bytes);
        buffer
    }

    #[cfg(test)]
    pub(crate) fn into_vec(self) -> Vec<u8> {
        if self.data.is_null() {
            return Vec::new();
        }

        unsafe { Vec::from_raw_parts(self.data, self.len, self.capacity) }
    }
}

pub(crate) fn free_byte_allocation(data: *mut u8, len: usize, capacity: usize) {
    if data.is_null() {
        return;
    }

    unsafe {
        let _ = Vec::from_raw_parts(data, len, capacity);
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn byte_buffer_empty_has_null_data_and_zero_sizes() {
        let buffer = ByteBuffer::empty();

        assert!(buffer.data.is_null());
        assert_eq!(buffer.len, 0);
        assert_eq!(buffer.capacity, 0);
    }

    #[test]
    fn byte_buffer_from_vec_transfers_allocation() {
        let original = vec![1, 2, 3, 4];
        let buffer = ByteBuffer::from_vec(original);

        assert!(!buffer.data.is_null());
        assert_eq!(buffer.len, 4);
        assert!(buffer.capacity >= buffer.len);

        let restored = unsafe { Vec::from_raw_parts(buffer.data, buffer.len, buffer.capacity) };
        assert_eq!(restored, vec![1, 2, 3, 4]);
    }

    #[test]
    fn screenshot_buffer_from_vec_records_image_metadata() {
        let original = vec![10, 20, 30, 40, 50, 60, 70, 80];
        let buffer = ScreenshotBuffer::from_vec(original, 1, 2, 4);

        assert!(!buffer.data.is_null());
        assert_eq!(buffer.len, 8);
        assert_eq!(buffer.width, 1);
        assert_eq!(buffer.height, 2);
        assert_eq!(buffer.stride, 4);

        free_byte_allocation(buffer.data, buffer.len, buffer.capacity);
    }

    #[test]
    fn virtual_screen_bounds_new_records_values() {
        let bounds = VirtualScreenBounds::new(-10, 20, 1920, 1080);

        assert_eq!(bounds.x, -10);
        assert_eq!(bounds.y, 20);
        assert_eq!(bounds.width, 1920);
        assert_eq!(bounds.height, 1080);
    }

    #[test]
    fn free_byte_allocation_accepts_null() {
        free_byte_allocation(std::ptr::null_mut(), 0, 0);
    }
}
