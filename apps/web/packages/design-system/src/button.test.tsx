import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Button } from "./button";

describe("Button", () => {
  it("renders a native button by default and fires onClick", async () => {
    const onClick = vi.fn();
    render(<Button onClick={onClick}>Save</Button>);

    const button = screen.getByRole("button", { name: "Save" });
    await userEvent.click(button);

    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it("disables the button and shows a spinner while loading", () => {
    render(<Button loading>Save</Button>);

    const button = screen.getByRole("button", { name: "Save" });
    expect(button).toBeDisabled();
    expect(button.querySelector(".animate-spin")).not.toBeNull();
  });

  it("does not fire onClick when disabled", async () => {
    const onClick = vi.fn();
    render(
      <Button disabled onClick={onClick}>
        Save
      </Button>,
    );

    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(onClick).not.toHaveBeenCalled();
  });

  it("forwards a ref to the underlying <button>", () => {
    const ref = vi.fn();
    render(<Button ref={ref}>Save</Button>);

    expect(ref).toHaveBeenCalledWith(expect.any(HTMLButtonElement));
  });

  describe("asChild", () => {
    it("renders the child element instead of a <button>, applying button classes to it", () => {
      render(
        <Button asChild variant="secondary">
          <a href="/cases/1">View case</a>
        </Button>,
      );

      expect(screen.queryByRole("button")).toBeNull();

      const link = screen.getByRole("link", { name: "View case" });
      expect(link).toHaveAttribute("href", "/cases/1");
      expect(link.className).toContain("inline-flex");
    });

    it("forwards the ref to the child element, not a wrapper", () => {
      const ref = vi.fn();
      render(
        <Button asChild ref={ref}>
          <a href="/cases/1">View case</a>
        </Button>,
      );

      expect(ref).toHaveBeenCalledWith(expect.any(HTMLAnchorElement));
    });

    it("lets the child own click behavior", async () => {
      const onClick = vi.fn();
      render(
        <Button asChild onClick={onClick}>
          <a href="/cases/1">View case</a>
        </Button>,
      );

      await userEvent.click(screen.getByRole("link", { name: "View case" }));

      expect(onClick).toHaveBeenCalledTimes(1);
    });
  });
});
