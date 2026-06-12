//! macOS Vision-based local OCR.
//!
//! Compiled only on macOS (gated by the `mod ocr` declaration in `lib.rs`).
//! Uses `VNRecognizeTextRequest`, which runs fully offline and synchronously.

use objc2::AnyThread;
use objc2::rc::{Retained, autoreleasepool};
use objc2::runtime::AnyObject;
use objc2_foundation::{NSArray, NSData, NSDictionary, NSString};
use objc2_vision::{
    VNImageRequestHandler, VNRecognizeTextRequest, VNRequest, VNRequestTextRecognitionLevel,
};

/// Recognize text in a PNG image using the Vision framework.
///
/// `languages` are BCP-47 codes in priority order (e.g. `["zh-Hans", "en-US"]`).
/// An empty slice enables automatic language detection.
pub fn recognize(png: &[u8], languages: &[String]) -> Result<String, String> {
    autoreleasepool(|_pool| {
        let request = VNRecognizeTextRequest::new();
        request.setRecognitionLevel(VNRequestTextRecognitionLevel::Accurate);
        request.setUsesLanguageCorrection(true);

        if languages.is_empty() {
            // Revision 3+ (macOS 13+) auto-detects script; a no-op on older revisions.
            request.setAutomaticallyDetectsLanguage(true);
        } else {
            let ns_languages: Vec<Retained<NSString>> = languages
                .iter()
                .map(|code| NSString::from_str(code))
                .collect();
            let languages_array = NSArray::from_retained_slice(&ns_languages);
            request.setRecognitionLanguages(&languages_array);
        }

        let image_data = NSData::with_bytes(png);
        let options = NSDictionary::<NSString, AnyObject>::new();
        let handler = VNImageRequestHandler::initWithData_options(
            VNImageRequestHandler::alloc(),
            &image_data,
            &options,
        );

        // `VNRecognizeTextRequest` derefs to its `VNRequest` superclass.
        let request_ref: &VNRequest = &request;
        let requests = NSArray::from_slice(&[request_ref]);
        handler
            .performRequests_error(&requests)
            .map_err(|err| format!("Vision performRequests failed: {err:?}"))?;

        let mut lines = String::new();
        if let Some(observations) = request.results() {
            for observation in observations.to_vec() {
                // Top candidate per recognized text region, joined as lines.
                let candidates = observation.topCandidates(1);
                if let Some(top) = candidates.to_vec().into_iter().next() {
                    if !lines.is_empty() {
                        lines.push('\n');
                    }
                    lines.push_str(&top.string().to_string());
                }
            }
        }

        Ok(lines)
    })
}

/// Returns the BCP-47 language codes supported at the accurate recognition level.
pub fn supported_languages() -> Result<Vec<String>, String> {
    autoreleasepool(|_pool| {
        let request = VNRecognizeTextRequest::new();
        request.setRecognitionLevel(VNRequestTextRecognitionLevel::Accurate);

        // SAFETY: `request` is a freshly created, valid `VNRecognizeTextRequest`.
        let languages = unsafe { request.supportedRecognitionLanguagesAndReturnError() }
            .map_err(|err| format!("Vision supportedRecognitionLanguages failed: {err:?}"))?;

        Ok(languages
            .to_vec()
            .iter()
            .map(|language| language.to_string())
            .collect())
    })
}
