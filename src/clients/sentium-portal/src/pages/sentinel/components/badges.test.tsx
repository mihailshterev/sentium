import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import EffectBadge from "./effect-badge";
import RiskBadge from "./risk-badge";
import AlignmentBadge from "./alignment-badge";
import type { PolicyRiskLevel } from "../../../types/sentinel";

describe("EffectBadge", () => {
  it("shows Allow when allowed", () => {
    render(<EffectBadge allowed effect="Allow" />);
    expect(screen.getByText("Allow")).toBeInTheDocument();
  });

  it("shows Alert for DenyWithAlert", () => {
    render(<EffectBadge allowed={false} effect="DenyWithAlert" />);
    expect(screen.getByText("Alert")).toBeInTheDocument();
  });

  it("shows Deny for other deny effects", () => {
    render(<EffectBadge allowed={false} effect="Deny" />);
    expect(screen.getByText("Deny")).toBeInTheDocument();
  });
});

describe("RiskBadge", () => {
  it.each(["Low", "Medium", "High", "Critical"] as PolicyRiskLevel[])("renders the %s risk level", (risk) => {
    render(<RiskBadge risk={risk} />);
    expect(screen.getByText(risk)).toBeInTheDocument();
  });
});

describe("AlignmentBadge", () => {
  it("renders a dash when there is no verdict", () => {
    render(<AlignmentBadge verdict={null} />);
    expect(screen.getByText("—")).toBeInTheDocument();
  });

  it("renders the verdict label", () => {
    render(<AlignmentBadge verdict="Aligned" />);
    expect(screen.getByText("Aligned")).toBeInTheDocument();
  });

  it("renders a neutral verdict", () => {
    render(<AlignmentBadge verdict="Uncertain" />);
    expect(screen.getByText("Uncertain")).toBeInTheDocument();
  });
});
