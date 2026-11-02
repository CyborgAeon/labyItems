import PDFKit
import AppKit

// Usage: extract-layout input.pdf output.json [preview.png]
guard CommandLine.arguments.count >= 3,
      let document = PDFDocument(url: URL(fileURLWithPath: CommandLine.arguments[1])) else {
    fatalError("Usage: extract-layout input.pdf output.json [preview.png]")
}
var pages: [[String: Any]] = []
for index in 0..<document.pageCount {
    guard let page = document.page(at: index), let text = page.string as NSString? else {
        fatalError("Page \(index + 1) has no extractable text")
    }
    var characters: [[String: Any]] = []
    for offset in 0..<text.length {
        let bounds = page.characterBounds(at: offset)
        characters.append([
            "t": text.substring(with: NSRange(location: offset, length: 1)),
            "x": bounds.origin.x, "y": bounds.origin.y,
            "w": bounds.width, "h": bounds.height
        ])
    }
    pages.append(["page": index + 1, "text": text, "chars": characters])
    if index == 0 && CommandLine.arguments.count > 3 {
        let image = page.thumbnail(of: NSSize(width: 900, height: 1200), for: .mediaBox)
        let bitmap = NSBitmapImageRep(data: image.tiffRepresentation!)!
        try bitmap.representation(using: .png, properties: [:])!.write(
            to: URL(fileURLWithPath: CommandLine.arguments[3]))
    }
}
try JSONSerialization.data(withJSONObject: pages, options: [.prettyPrinted]).write(
    to: URL(fileURLWithPath: CommandLine.arguments[2]))
