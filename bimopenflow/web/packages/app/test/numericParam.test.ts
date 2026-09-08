import { describe,it,expect } from "vitest";
import { numericValue,numericLimits,numericDisplay } from "../src/numericParam";
describe("bounded parameter types",()=>{
  it("preserves exact Int64 values in generic integer controls",()=>{
    expect(numericValue("Integer","9223372036854775807")).toBe("9223372036854775807");
    expect(numericDisplay("9223372036854775807")).toBe("9223372036854775807");
  });
  it("clamps fractions and percentages and rejects empty/nonfinite edits",()=>{
    expect(numericValue("Fraction","2")).toBe("1");
    expect(numericValue("Fraction","-1")).toBe("0");
    expect(numericValue("Percent","150")).toBe("100");
    expect(numericValue("Percent","-1")).toBe("0");
    for(const value of ["","NaN","Infinity"]) expect(numericValue("Fraction",value)).toBeNull();
  });
  it("prevents display hints widening a type domain",()=>{
    expect(numericLimits("Fraction",{kind:"slider",min:-5,max:5})).toMatchObject({min:0,max:1});
    expect(numericValue("Fraction","0",{kind:"slider",min:.01,max:1,step:.01})).toBe("0.01");
  });
  it("displays normalized fractions as percent without changing stored values",()=>{
    expect(numericDisplay("0.25",{kind:"slider",unit:"percent"})).toBe("25");
    expect(numericValue("Fraction",String(25/100))).toBe("0.25");
    expect(numericDisplay("25",{kind:"slider"})).toBe("25");
  });
});
