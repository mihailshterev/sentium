/** Width and height of uploaded profile pictures, enough for a sharp 64px avatar on high-density screens. */
export const AVATAR_PIXELS = 256;

const WEBP_QUALITY = 0.85;
const JPEG_QUALITY = 0.88;

function canvasToBlob(canvas: HTMLCanvasElement, type: string, quality: number): Promise<Blob | null> {
  return new Promise((resolve) => canvas.toBlob(resolve, type, quality));
}

/**
 * Center-crops an image to a square and scales it to {@link AVATAR_PIXELS}, re-encoded as WebP (JPEG where the
 * browser cannot encode WebP). Re-encoding also drops EXIF data such as the location a photo was taken.
 * Rejects when the browser cannot decode the file.
 */
export async function toAvatarImage(file: Blob): Promise<Blob> {
  const bitmap = await createImageBitmap(file, { imageOrientation: "from-image" }).catch(() => {
    throw new Error("This browser cannot read that image. Choose a JPEG, PNG or WebP picture.");
  });

  try {
    const side = Math.min(bitmap.width, bitmap.height);
    const canvas = document.createElement("canvas");
    canvas.width = AVATAR_PIXELS;
    canvas.height = AVATAR_PIXELS;

    const context = canvas.getContext("2d");
    if (!context) {
      throw new Error("Canvas is not available.");
    }

    context.imageSmoothingQuality = "high";
    context.drawImage(
      bitmap,
      (bitmap.width - side) / 2,
      (bitmap.height - side) / 2,
      side,
      side,
      0,
      0,
      AVATAR_PIXELS,
      AVATAR_PIXELS,
    );

    // Browsers that cannot encode WebP silently return a PNG instead.
    const webp = await canvasToBlob(canvas, "image/webp", WEBP_QUALITY);
    if (webp?.type === "image/webp") {
      return webp;
    }

    // JPEG has no transparency: put transparent pixels on white rather than black.
    context.globalCompositeOperation = "destination-over";
    context.fillStyle = "#fff";
    context.fillRect(0, 0, AVATAR_PIXELS, AVATAR_PIXELS);

    const jpeg = await canvasToBlob(canvas, "image/jpeg", JPEG_QUALITY);
    if (!jpeg) {
      throw new Error("The picture could not be encoded.");
    }

    return jpeg;
  } finally {
    bitmap.close();
  }
}
