import { render } from "@testing-library/react"
import { afterEach, beforeEach, expect, test, vi } from "vitest"
import { UploadedImagePreview } from "./UploadedImagePreview"

const createObjectURL = vi.fn(() => "blob:preview")
const revokeObjectURL = vi.fn()

beforeEach(() => {
    URL.createObjectURL = createObjectURL
    URL.revokeObjectURL = revokeObjectURL
})

afterEach(() => {
    vi.clearAllMocks()
})

const photo = () => new File(["x"], "photo.png", { type: "image/png" })

test("creates one object URL however often it re-renders", () => {
    const image = photo()
    const { rerender } = render(<UploadedImagePreview image={image} />)

    rerender(<UploadedImagePreview image={image} className="a" />)
    rerender(<UploadedImagePreview image={image} className="b" />)

    expect(createObjectURL).toHaveBeenCalledTimes(1)
    expect(revokeObjectURL).not.toHaveBeenCalled()
})

test("revokes the object URL on unmount", () => {
    const { unmount } = render(<UploadedImagePreview image={photo()} />)

    unmount()

    expect(revokeObjectURL).toHaveBeenCalledWith("blob:preview")
})
