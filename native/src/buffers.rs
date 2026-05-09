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
}

pub(crate) fn free_byte_allocation(data: *mut u8, len: usize, capacity: usize) {
    if data.is_null() {
        return;
    }

    unsafe {
        let _ = Vec::from_raw_parts(data, len, capacity);
    }
}
