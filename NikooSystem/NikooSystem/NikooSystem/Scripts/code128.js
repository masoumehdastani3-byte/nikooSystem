/* Code 128 set B: encodes the product code (ASCII 32–126) into a printable barcode. */
var nikooCode128Patterns = [
    "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
    "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
    "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
    "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
    "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
    "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
    "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
    "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
    "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
    "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
    "114131", "311141", "411131", "211412", "211214", "211232", "2331112"
];

function nikooCode128Svg(text) {
    if (text == null) return null;
    text = String(text).trim();
    if (!text || text.length > 40) return null;
    var codes = [104];
    var sum = 104;
    for (var i = 0; i < text.length; i++) {
        var value = text.charCodeAt(i) - 32;
        if (value < 0 || value > 94) return null;
        codes.push(value);
        sum += value * (i + 1);
    }
    codes.push(sum % 103);
    codes.push(106);

    var pattern = "";
    for (var c = 0; c < codes.length; c++) pattern += nikooCode128Patterns[codes[c]];

    var quiet = 10;
    var modules = quiet * 2;
    for (var p = 0; p < pattern.length; p++) modules += parseInt(pattern.charAt(p), 10);

    var x = quiet;
    var rects = "";
    var drawBar = true;
    for (var n = 0; n < pattern.length; n++) {
        var width = parseInt(pattern.charAt(n), 10);
        if (drawBar) rects += '<rect x="' + x + '" y="0" width="' + width + '" height="40"/>';
        x += width;
        drawBar = !drawBar;
    }
    return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ' + modules + ' 40" preserveAspectRatio="none" shape-rendering="crispEdges" role="img">' + rects + '</svg>';
}
