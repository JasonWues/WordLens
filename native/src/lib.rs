#[unsafe(no_mangle)]
pub extern "C" fn get_selection_text() -> *mut std::ffi::c_char {
    let text: String = selection::get_text();
    let sanitized = text.replace('\0', "");
    match std::ffi::CString::new(sanitized) {
        Ok(cstr) => cstr.into_raw(),
        Err(_) => std::ptr::null_mut(),
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn free_c_string(ptr: *mut std::ffi::c_char) {
    if ptr.is_null() {
        return;
    }
    unsafe {
        let _ = std::ffi::CString::from_raw(ptr);
    }
}
